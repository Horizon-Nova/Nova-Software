using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.IsolatedStorage;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using ExternalModules.InSpecTool.Model;
using Microsoft.Extensions.Logging;

namespace ExternalModules.InSpecTool.Infrastructure;

/// <summary>
/// 封裝 InSpecTool 所需的檔案系統、Win32 與外部程式互動能力。
/// </summary>
internal sealed class InSpecToolInfrastructure
{
    private const uint AutomationWmSetText = 0x000C;
    private const uint AutomationBmClick = 0x00F5;
    private const uint AutomationGwOwner = 4;
    private const uint MenuWmCommand = 0x0111;
    private const uint MenuMfByPosition = 0x0400;
    private const uint MenuMfByCommand = 0x0000;
    private const uint MenuMfGrayed = 0x0001;
    private const uint MenuMfDisabled = 0x0002;
    private const int DefaultAutomationDebounceMs = 600;
    private const string BridgeIsolatedStorageDirectoryName = "InSpecTool";
    private const string BridgeCurrentTargetFileName = "current-target.txt";

    private static readonly string BridgeBaseDirectory = @"C:\InSpecTool";
    private static readonly string BridgeInputFilePath = Path.Combine(BridgeBaseDirectory, "Input.txt");
    private static readonly string BridgeLegacyCurrentTargetFilePath = Path.Combine(BridgeBaseDirectory, ".current_target.txt");
    private static readonly string[] ConfirmButtonTexts = ["OK", "確定"];
    private static readonly string[] PlaybackMenuCandidates = ["playback", "auto measure", "自動量測"];
    private static readonly string[] RunMenuCandidates = ["run", "執行"];

    private readonly object _fileQuerySyncRoot = new();
    private readonly object _automationSyncRoot = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _automationTriggeredAt = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _bridgeLock = new(1, 1);

    private string _indexedRootDirectory = string.Empty;
    private List<IndexedFileEntry> _indexedEntries = [];
    private InSpecToolConfigurationModel _automationConfiguration = new();
    private nint _lastTriggeredDialogHandle;
    private FileSystemWatcher? _bridgeWatcher;
    private bool _isBridgeActive;
    private string _currentTargetPath = string.Empty;
    private Action<string, LogLevel>? _writeLog;
    private CancellationTokenSource? _automationCancellation;
    private Task? _automationTask;

    /// <summary>
    /// 當偵測到需要自動輸入的欄位時引發。
    /// </summary>
    public event EventHandler<string>? AutoInputRequested;

    /// <summary>
    /// 套用宿主提供的記錄函式。
    /// </summary>
    /// <param name="writeLog">宿主記錄函式。</param>
    public void ApplyLogger(Action<string, LogLevel>? writeLog)
    {
        _writeLog = writeLog;
    }

    /// <summary>
    /// 刪除檔案查詢索引快取。
    /// </summary>
    public void DeleteFileQueryIndex()
    {
        lock (_fileQuerySyncRoot)
        {
            _indexedRootDirectory = string.Empty;
            _indexedEntries = [];
        }
    }

    /// <summary>
    /// 查詢符合條件的 InSpec 檔案。
    /// </summary>
    /// <param name="searchDirectory">搜尋根目錄。</param>
    /// <param name="rawPartNumber">原始料號。</param>
    /// <param name="normalizedPartNumber">正規化料號。</param>
    /// <param name="setupKeyword">必要關鍵字。</param>
    /// <returns>符合條件的檔案結果集合。</returns>
    public IReadOnlyList<InSpecPreviewResultModel> QueryFiles(string searchDirectory, string rawPartNumber, string normalizedPartNumber, string setupKeyword)
    {
        var rootDirectory = searchDirectory.Trim();
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new InvalidOperationException("尚未設定搜尋資料夾。");
        }

        LoadFileIndex(rootDirectory);

        var queryTokens = new[] { rawPartNumber, normalizedPartNumber }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (queryTokens.Length == 0)
        {
            return [];
        }

        var requiredKeyword = setupKeyword.Trim().ToUpperInvariant();
        List<IndexedFileEntry> matches;
        lock (_fileQuerySyncRoot)
        {
            matches = _indexedEntries
                .Where(entry =>
                    (string.IsNullOrWhiteSpace(requiredKeyword) || entry.FileNameUpper.Contains(requiredKeyword, StringComparison.Ordinal)) &&
                    queryTokens.Any(token => entry.FileNameUpper.Contains(token, StringComparison.Ordinal)))
                .OrderByDescending(entry => entry.LastWriteTimeUtc)
                .ToList();
        }

        return matches
            .Select(entry => new InSpecPreviewResultModel
            {
                FileName = entry.FileName,
                FilePath = entry.FilePath,
                PartNumber = normalizedPartNumber,
                Revision = QueryRevisionText(entry.FileName)
            })
            .ToArray();
    }

    /// <summary>
    /// 套用自動監控組態。
    /// </summary>
    /// <param name="configuration">目前組態。</param>
    public void ApplyAutomationConfiguration(InSpecToolConfigurationModel configuration)
    {
        lock (_automationSyncRoot)
        {
            _automationConfiguration = configuration;
        }
    }

    /// <summary>
    /// 啟用自動監控。
    /// </summary>
    public void ActivateAutomationMonitoring()
    {
        lock (_automationSyncRoot)
        {
            if (_automationTask is { IsCompleted: false })
            {
                return;
            }

            _automationCancellation = new CancellationTokenSource();
            _automationTask = Task.Run(() => ApplyAutomationLoopAsync(_automationCancellation.Token));
        }
    }

    /// <summary>
    /// 停用自動監控。
    /// </summary>
    public void DeactivateAutomationMonitoring()
    {
        CancellationTokenSource? cancellation;
        Task? monitoringTask;

        lock (_automationSyncRoot)
        {
            cancellation = _automationCancellation;
            monitoringTask = _automationTask;
            _automationCancellation = null;
            _automationTask = null;
        }

        cancellation?.Cancel();
        try
        {
            monitoringTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException exception)
        {
            var detailText = exception.Flatten().InnerExceptions.FirstOrDefault()?.Message ?? exception.Message;
            _writeLog?.Invoke($"停止 InSpec 自動監控時發生例外：{detailText}", LogLevel.Warning);
        }
        finally
        {
            cancellation?.Dispose();
        }
    }

    /// <summary>
    /// 套用自動輸入文字至 InSpec 視窗。
    /// </summary>
    /// <param name="value">欲輸入的文字。</param>
    /// <returns>若輸入成功則回傳 <c>true</c>。</returns>
    public bool ValidateAutoInput(string value)
    {
        var dialogHandle = Win32Automation.GetForegroundWindow();
        if (dialogHandle == nint.Zero)
        {
            return false;
        }

        var inputHandle = QueryFirstChildByClassName(dialogHandle, "Edit");
        if (inputHandle == nint.Zero)
        {
            return false;
        }

        Win32Automation.SetForegroundWindow(dialogHandle);
        Thread.Sleep(100);

        if (!Win32Automation.ValidateSendMessage(inputHandle, AutomationWmSetText, nint.Zero, value ?? string.Empty))
        {
            return false;
        }

        Thread.Sleep(100);
        var confirmButtonHandle = QueryConfirmButtonHandle(dialogHandle);
        if (confirmButtonHandle == nint.Zero)
        {
            return false;
        }

        return Win32Automation.ValidateSendMessage(confirmButtonHandle, AutomationBmClick, nint.Zero, nint.Zero);
    }

    /// <summary>
    /// 啟用檔案橋接監聽。
    /// </summary>
    public void ActivateFileBridge()
    {
        if (_isBridgeActive)
        {
            return;
        }

        CreateBridgeFiles();
        _bridgeWatcher = new FileSystemWatcher(BridgeBaseDirectory, "Input.txt")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _bridgeWatcher.Changed += ApplyBridgeWatcherChanged;
        _bridgeWatcher.Created += ApplyBridgeWatcherChanged;
        _isBridgeActive = true;
        _ = ApplyBridgeInputAsync();
    }

    /// <summary>
    /// 停用檔案橋接監聽。
    /// </summary>
    public void DeactivateFileBridge()
    {
        if (!_isBridgeActive)
        {
            return;
        }

        if (_bridgeWatcher is not null)
        {
            _bridgeWatcher.EnableRaisingEvents = false;
            _bridgeWatcher.Changed -= ApplyBridgeWatcherChanged;
            _bridgeWatcher.Created -= ApplyBridgeWatcherChanged;
            _bridgeWatcher.Dispose();
            _bridgeWatcher = null;
        }

        _isBridgeActive = false;
    }

    /// <summary>
    /// 更新橋接輸出的目前目標路徑。
    /// </summary>
    /// <param name="targetPath">目標路徑。</param>
    public void UpdateCurrentTargetPath(string? targetPath)
    {
        _currentTargetPath = string.IsNullOrWhiteSpace(targetPath) ? string.Empty : targetPath.Trim();
        SaveCurrentTargetPath(_currentTargetPath);
        DeleteLegacyCurrentTargetFile();
    }

    /// <summary>
    /// 開啟指定結果檔案，必要時自動觸發 Run。
    /// </summary>
    /// <param name="resultModel">檔案結果模型。</param>
    /// <param name="enableAutoRun">是否啟用自動執行。</param>
    public void OpenFile(InSpecPreviewResultModel resultModel, bool enableAutoRun)
    {
        if (!File.Exists(resultModel.FilePath))
        {
            throw new FileNotFoundException("找不到指定的 InSpec 檔案。", resultModel.FilePath);
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = resultModel.FilePath,
                UseShellExecute = true
            });

            ApplyAutoRunAfterOpen(enableAutoRun);
        }
        catch
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{resultModel.FilePath}\"",
                UseShellExecute = true
            });
        }
    }

    /// <summary>
    /// 釋放基礎設施層資源。
    /// </summary>
    public void Dispose()
    {
        DeactivateAutomationMonitoring();
        DeactivateFileBridge();
        _bridgeLock.Dispose();
    }

    private void LoadFileIndex(string rootDirectory)
    {
        var normalizedRootDirectory = Path.GetFullPath(rootDirectory);
        lock (_fileQuerySyncRoot)
        {
            if (string.Equals(_indexedRootDirectory, normalizedRootDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!Directory.Exists(normalizedRootDirectory))
            {
                throw new DirectoryNotFoundException($"找不到搜尋資料夾：{normalizedRootDirectory}");
            }

            _indexedEntries = Directory
                .EnumerateFiles(normalizedRootDirectory, "*.iwp", SearchOption.AllDirectories)
                .Select(filePath =>
                {
                    var fileInfo = new FileInfo(filePath);
                    return new IndexedFileEntry
                    {
                        FilePath = fileInfo.FullName,
                        FileName = fileInfo.Name,
                        FileNameUpper = fileInfo.Name.ToUpperInvariant(),
                        LastWriteTimeUtc = fileInfo.LastWriteTimeUtc
                    };
                })
                .ToList();

            _indexedRootDirectory = normalizedRootDirectory;
        }
    }

    private async Task ApplyAutomationLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var inputTypeName = QueryAutoInputRequest();
            if (!string.IsNullOrWhiteSpace(inputTypeName))
            {
                AutoInputRequested?.Invoke(this, inputTypeName);
            }

            try
            {
                await Task.Delay(QueryMonitorIntervalMs(), cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private InSpecToolConfigurationModel QueryAutomationConfiguration()
    {
        lock (_automationSyncRoot)
        {
            return _automationConfiguration;
        }
    }

    private int QueryMonitorIntervalMs()
    {
        var configuration = QueryAutomationConfiguration();
        return Math.Clamp(configuration.MonitorIntervalMs, 100, 10000);
    }

    private string? QueryAutoInputRequest()
    {
        var configuration = QueryAutomationConfiguration();
        if (!configuration.EnableAutoMonitoring)
        {
            return null;
        }

        var promptMatch = QueryPromptMatch(configuration.FieldDefinitions);
        if (promptMatch is null)
        {
            return null;
        }

        if (!ValidateAutomationDebounce(promptMatch.Value.InputTypeName, promptMatch.Value.DialogHandle))
        {
            return null;
        }

        return promptMatch.Value.InputTypeName;
    }

    private bool ValidateAutomationDebounce(string inputTypeName, nint dialogHandle)
    {
        if (dialogHandle != nint.Zero && dialogHandle == _lastTriggeredDialogHandle)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        if (_automationTriggeredAt.TryGetValue(inputTypeName, out var previousTriggeredAt) &&
            (now - previousTriggeredAt).TotalMilliseconds < DefaultAutomationDebounceMs)
        {
            return false;
        }

        _automationTriggeredAt[inputTypeName] = now;
        _lastTriggeredDialogHandle = dialogHandle;
        return true;
    }

    private (string InputTypeName, nint DialogHandle)? QueryPromptMatch(IReadOnlyList<InSpecFieldDefinitionModel> fieldDefinitions)
    {
        var dialogHandle = Win32Automation.GetForegroundWindow();
        if (dialogHandle == nint.Zero || !ValidateBelongsToInSpec(dialogHandle))
        {
            return null;
        }

        var promptText = QueryPromptText(dialogHandle);
        if (string.IsNullOrWhiteSpace(promptText))
        {
            return null;
        }

        foreach (var fieldDefinition in fieldDefinitions)
        {
            if (string.IsNullOrWhiteSpace(fieldDefinition.Keyword))
            {
                continue;
            }

            if (promptText.Contains(fieldDefinition.Keyword.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return (fieldDefinition.Name, dialogHandle);
            }
        }

        return null;
    }

    private bool ValidateBelongsToInSpec(nint dialogHandle)
    {
        var mainWindowHandle = QueryInSpecMainWindowHandle();
        if (mainWindowHandle == nint.Zero)
        {
            return false;
        }

        var currentHandle = dialogHandle;
        while (currentHandle != nint.Zero)
        {
            if (currentHandle == mainWindowHandle)
            {
                return true;
            }

            currentHandle = Win32Automation.GetWindow(currentHandle, AutomationGwOwner);
        }

        return false;
    }

    private nint QueryInSpecMainWindowHandle()
    {
        nint mainWindowHandle = nint.Zero;
        Win32Automation.EnumWindows((windowHandle, _) =>
        {
            if (!Win32Automation.IsWindowVisible(windowHandle))
            {
                return true;
            }

            var className = Win32Automation.QueryClassName(windowHandle);
            var title = Win32Automation.QueryWindowText(windowHandle).Trim();
            if (string.Equals(className, "InSpecAppFrame", StringComparison.OrdinalIgnoreCase) ||
                title.StartsWith("InSpec for Windows", StringComparison.OrdinalIgnoreCase))
            {
                mainWindowHandle = windowHandle;
                return false;
            }

            return true;
        }, nint.Zero);

        return mainWindowHandle;
    }

    private string QueryPromptText(nint dialogHandle)
    {
        var bestText = string.Empty;
        Win32Automation.EnumChildWindows(dialogHandle, (childHandle, _) =>
        {
            if (!string.Equals(Win32Automation.QueryClassName(childHandle), "Static", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var text = Win32Automation.QueryWindowText(childHandle).Trim();
            if (text.Length > bestText.Length)
            {
                bestText = text;
            }

            return true;
        }, nint.Zero);

        return bestText;
    }

    private nint QueryFirstChildByClassName(nint parentHandle, string className)
    {
        nint childHandle = nint.Zero;
        Win32Automation.EnumChildWindows(parentHandle, (currentHandle, _) =>
        {
            if (string.Equals(Win32Automation.QueryClassName(currentHandle), className, StringComparison.OrdinalIgnoreCase))
            {
                childHandle = currentHandle;
                return false;
            }

            return true;
        }, nint.Zero);

        return childHandle;
    }

    private nint QueryConfirmButtonHandle(nint dialogHandle)
    {
        nint buttonHandle = nint.Zero;
        Win32Automation.EnumChildWindows(dialogHandle, (childHandle, _) =>
        {
            if (!string.Equals(Win32Automation.QueryClassName(childHandle), "Button", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var buttonText = Win32Automation.QueryWindowText(childHandle).Trim();
            if (ConfirmButtonTexts.Any(text => string.Equals(text, buttonText, StringComparison.OrdinalIgnoreCase)))
            {
                buttonHandle = childHandle;
                return false;
            }

            return true;
        }, nint.Zero);

        return buttonHandle;
    }

    private void CreateBridgeFiles()
    {
        Directory.CreateDirectory(BridgeBaseDirectory);
        CreateEmptyFile(BridgeInputFilePath, false);
        DeleteLegacyCurrentTargetFile();
    }

    private void ApplyBridgeWatcherChanged(object sender, FileSystemEventArgs eventArgs)
    {
        _ = ApplyBridgeInputAsync();
    }

    private async Task ApplyBridgeInputAsync()
    {
        if (!_isBridgeActive)
        {
            return;
        }

        await _bridgeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(BridgeInputFilePath))
            {
                CreateEmptyFile(BridgeInputFilePath, false);
                return;
            }

            var inputBytes = await File.ReadAllBytesAsync(BridgeInputFilePath).ConfigureAwait(false);
            if (inputBytes.Length == 0)
            {
                return;
            }

            var contentBytes = ResolveBridgeContentBytes(inputBytes);
            if (contentBytes.Length == 0)
            {
                await File.WriteAllBytesAsync(BridgeInputFilePath, []).ConfigureAwait(false);
                return;
            }

            var targetPath = QueryCurrentTargetPath();
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return;
            }

            await AppendContentToTargetFileAsync(targetPath, contentBytes).ConfigureAwait(false);
            await File.WriteAllBytesAsync(BridgeInputFilePath, []).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            _writeLog?.Invoke($"同步 InSpec 橋接輸入檔時發生 I/O 例外：{exception.Message}", LogLevel.Warning);
            ApplyBridgeRecovery();
        }
        catch (UnauthorizedAccessException exception)
        {
            _writeLog?.Invoke($"同步 InSpec 橋接輸入檔時發生權限例外：{exception.Message}", LogLevel.Warning);
            ApplyBridgeRecovery();
        }
        catch (Exception exception)
        {
            _writeLog?.Invoke($"同步 InSpec 橋接輸入檔時發生未預期例外：{exception.Message}", LogLevel.Error);
            ApplyBridgeRecovery();
        }
        finally
        {
            _bridgeLock.Release();
        }
    }

    private void ApplyBridgeRecovery()
    {
        try
        {
            CreateEmptyFile(BridgeInputFilePath, true);
        }
        catch (IOException exception)
        {
            _writeLog?.Invoke($"清除 InSpec 橋接輸入檔失敗：{exception.Message}", LogLevel.Warning);
        }
        catch (UnauthorizedAccessException exception)
        {
            _writeLog?.Invoke($"清除 InSpec 橋接輸入檔時權限不足：{exception.Message}", LogLevel.Warning);
        }
    }

    private async Task AppendContentToTargetFileAsync(string targetPath, byte[] contentBytes)
    {
        var parentDirectory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(parentDirectory))
        {
            Directory.CreateDirectory(parentDirectory);
        }

        await using var stream = new FileStream(targetPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        await stream.WriteAsync(contentBytes).ConfigureAwait(false);
    }

    private byte[] ResolveBridgeContentBytes(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return bytes[2..];
        }

        return bytes;
    }

    private string QueryCurrentTargetPath()
    {
        if (!string.IsNullOrWhiteSpace(_currentTargetPath))
        {
            SaveCurrentTargetPath(_currentTargetPath);
            return _currentTargetPath;
        }

        _currentTargetPath = LoadCurrentTargetPath();
        return _currentTargetPath;
    }

    private string LoadCurrentTargetPath()
    {
        using var store = IsolatedStorageFile.GetUserStoreForAssembly();
        var statePath = QueryBridgeStatePath();
        if (!store.FileExists(statePath))
        {
            return string.Empty;
        }

        using var stream = new IsolatedStorageFileStream(statePath, FileMode.Open, FileAccess.Read, store);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }

    private void SaveCurrentTargetPath(string targetPath)
    {
        using var store = IsolatedStorageFile.GetUserStoreForAssembly();
        if (!store.DirectoryExists(BridgeIsolatedStorageDirectoryName))
        {
            store.CreateDirectory(BridgeIsolatedStorageDirectoryName);
        }

        using var stream = new IsolatedStorageFileStream(QueryBridgeStatePath(), FileMode.Create, FileAccess.Write, store);
        using var writer = new StreamWriter(stream);
        writer.Write(targetPath ?? string.Empty);
    }

    private void DeleteLegacyCurrentTargetFile()
    {
        if (File.Exists(BridgeLegacyCurrentTargetFilePath))
        {
            File.Delete(BridgeLegacyCurrentTargetFilePath);
        }
    }

    private string QueryBridgeStatePath() => $"{BridgeIsolatedStorageDirectoryName}/{BridgeCurrentTargetFileName}";

    private void CreateEmptyFile(string path, bool truncate)
    {
        if (truncate || !File.Exists(path))
        {
            File.WriteAllBytes(path, []);
        }
    }

    private void ApplyAutoRunAfterOpen(bool enableAutoRun)
    {
        if (!enableAutoRun)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(600).ConfigureAwait(false);
                ValidateAutoRunMenuClick();
            }
            catch
            {
            }
        });
    }

    private bool ValidateAutoRunMenuClick()
    {
        var mainWindowHandle = QueryInSpecMainWindowHandle();
        if (mainWindowHandle == nint.Zero)
        {
            return false;
        }

        Win32Menu.SetForegroundWindow(mainWindowHandle);
        Thread.Sleep(200);

        var menuHandle = Win32Menu.GetMenu(mainWindowHandle);
        if (menuHandle == nint.Zero)
        {
            return false;
        }

        var runCommandId = QueryRunCommandId(menuHandle);
        if (runCommandId <= 0)
        {
            return false;
        }

        var startAt = Environment.TickCount64;
        while (Environment.TickCount64 - startAt < 5000)
        {
            var state = Win32Menu.GetMenuState(menuHandle, (uint)runCommandId, MenuMfByCommand);
            if (state != uint.MaxValue)
            {
                var isDisabled = (state & (MenuMfGrayed | MenuMfDisabled)) != 0;
                if (!isDisabled)
                {
                    break;
                }
            }

            Thread.Sleep(150);
        }

        Win32Menu.SendMessage(mainWindowHandle, MenuWmCommand, new nint(runCommandId), nint.Zero);
        return true;
    }

    private int QueryRunCommandId(nint menuHandle)
    {
        var menuCount = Win32Menu.GetMenuItemCount(menuHandle);
        if (menuCount <= 0)
        {
            return 0;
        }

        for (var menuIndex = 0; menuIndex < menuCount; menuIndex++)
        {
            var menuText = ResolveMenuText(LoadMenuText(menuHandle, (uint)menuIndex));
            if (!ValidateMenuText(menuText, PlaybackMenuCandidates))
            {
                continue;
            }

            var subMenuHandle = Win32Menu.GetSubMenu(menuHandle, menuIndex);
            if (subMenuHandle == nint.Zero)
            {
                continue;
            }

            var subMenuCount = Win32Menu.GetMenuItemCount(subMenuHandle);
            if (subMenuCount <= 0)
            {
                continue;
            }

            for (var itemIndex = 0; itemIndex < subMenuCount; itemIndex++)
            {
                var itemText = ResolveMenuText(LoadMenuText(subMenuHandle, (uint)itemIndex));
                if (!ValidateMenuText(itemText, RunMenuCandidates))
                {
                    continue;
                }

                var commandId = Win32Menu.GetMenuItemID(subMenuHandle, itemIndex);
                if (commandId > 0)
                {
                    return commandId;
                }
            }
        }

        return 0;
    }

    private string LoadMenuText(nint menuHandle, uint itemIndex)
    {
        var builder = new StringBuilder(512);
        var copied = Win32Menu.GetMenuString(menuHandle, itemIndex, builder, builder.Capacity, MenuMfByPosition);
        return copied <= 0 ? string.Empty : builder.ToString();
    }

    private string ResolveMenuText(string text)
    {
        return text
            .Replace("&", string.Empty, StringComparison.Ordinal)
            .Split('\t')[0]
            .Trim()
            .ToLowerInvariant();
    }

    private bool ValidateMenuText(string normalizedText, IEnumerable<string> candidates)
        => candidates.Any(candidate => normalizedText.Contains(candidate, StringComparison.OrdinalIgnoreCase));

    private string QueryRevisionText(string fileName)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var match = Regex.Match(fileNameWithoutExtension, @"(?i)\brev[.\-_ ]?([A-Z0-9]+)\b");
        return match.Success ? $"Rev.{match.Groups[1].Value.ToUpperInvariant()}" : string.Empty;
    }

    private sealed class IndexedFileEntry
    {
        public string FilePath { get; init; } = string.Empty;

        public string FileName { get; init; } = string.Empty;

        public string FileNameUpper { get; init; } = string.Empty;

        public DateTime LastWriteTimeUtc { get; init; }
    }

    private static class Win32Automation
    {
        internal delegate bool EnumWindowsProc(nint windowHandle, nint lParam);

        [DllImport("user32.dll")]
        internal static extern nint GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool EnumChildWindows(nint parentHandle, EnumWindowsProc callback, nint lParam);

        [DllImport("user32.dll")]
        internal static extern bool IsWindowVisible(nint windowHandle);

        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(nint windowHandle);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern nint SendMessageW(nint windowHandle, uint message, nint wParam, string lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern nint SendMessageW(nint windowHandle, uint message, nint wParam, nint lParam);

        [DllImport("user32.dll")]
        internal static extern nint GetWindow(nint windowHandle, uint command);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowTextW(nint windowHandle, StringBuilder buffer, int maxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowTextLengthW(nint windowHandle);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetClassNameW(nint windowHandle, StringBuilder buffer, int maxCount);

        internal static bool ValidateSendMessage(nint windowHandle, uint message, nint wParam, string value)
            => SendMessageW(windowHandle, message, wParam, value) != nint.Zero || message == AutomationWmSetText;

        internal static bool ValidateSendMessage(nint windowHandle, uint message, nint wParam, nint lParam)
            => SendMessageW(windowHandle, message, wParam, lParam) != nint.Zero || message == AutomationBmClick;

        internal static string QueryWindowText(nint windowHandle)
        {
            var textLength = GetWindowTextLengthW(windowHandle);
            if (textLength <= 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(textLength + 1);
            _ = GetWindowTextW(windowHandle, builder, builder.Capacity);
            return builder.ToString();
        }

        internal static string QueryClassName(nint windowHandle)
        {
            var builder = new StringBuilder(256);
            _ = GetClassNameW(windowHandle, builder, builder.Capacity);
            return builder.ToString();
        }
    }

    private static class Win32Menu
    {
        internal delegate bool EnumWindowsProc(nint windowHandle, nint lParam);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

        [DllImport("user32.dll")]
        internal static extern bool IsWindowVisible(nint windowHandle);

        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(nint windowHandle);

        [DllImport("user32.dll")]
        internal static extern nint GetMenu(nint windowHandle);

        [DllImport("user32.dll")]
        internal static extern int GetMenuItemCount(nint menuHandle);

        [DllImport("user32.dll")]
        internal static extern nint GetSubMenu(nint menuHandle, int position);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetMenuString(nint menuHandle, uint itemId, StringBuilder buffer, int maxCount, uint flags);

        [DllImport("user32.dll")]
        internal static extern int GetMenuItemID(nint menuHandle, int position);

        [DllImport("user32.dll")]
        internal static extern uint GetMenuState(nint menuHandle, uint itemId, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern nint SendMessage(nint windowHandle, uint message, nint wParam, nint lParam);
    }
}

