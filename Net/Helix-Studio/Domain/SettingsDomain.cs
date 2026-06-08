using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Helix_Studio.Data.Repositories;
using Helix_Studio.Infrastructure.Licensing;
using Helix_Studio.Infrastructure.Paths;
using Helix_Studio.Model;

namespace Helix_Studio.Domain;

/// <summary>
/// 管理設定頁的資料讀寫、工作區操作與關於資訊。
/// </summary>
public sealed class SettingsDomain
{
    private const string MainSection = "Main";
    private const string SettingsSection = "Settings";
    private const string DefaultHomePageId = "overview";

    private static readonly IReadOnlyDictionary<int, TimeSpan> InactivityTimeoutByIndex =
        new Dictionary<int, TimeSpan>
        {
            [1] = TimeSpan.FromMinutes(5),
            [2] = TimeSpan.FromMinutes(15),
            [3] = TimeSpan.FromMinutes(30),
            [4] = TimeSpan.FromHours(1),
        };

    private readonly IniRepository _iniRepository;
    private readonly WorkspacesRepository _workspacesRepository;
    private readonly IApplicationLicenseService _applicationLicenseService;

    /// <summary>
    /// 建立設定 Domain。
    /// </summary>
    public SettingsDomain(
        IniRepository iniRepository,
        WorkspacesRepository workspacesRepository,
        IApplicationLicenseService applicationLicenseService)
    {
        _iniRepository = iniRepository;
        _workspacesRepository = workspacesRepository;
        _applicationLicenseService = applicationLicenseService;
    }

    /// <summary>
    /// 設定變更事件。
    /// </summary>
    public event EventHandler? SettingsChanged
    {
        add => _iniRepository.Changed += value;
        remove => _iniRepository.Changed -= value;
    }

    /// <summary>
    /// 工作區變更事件。
    /// </summary>
    public event EventHandler? WorkspacesChanged
    {
        add => _workspacesRepository.Changed += value;
        remove => _workspacesRepository.Changed -= value;
    }

    /// <summary>
    /// 讀取設定快照。
    /// </summary>
    public SettingsModel QuerySettings()
        => _iniRepository.QuerySnapshot<SettingsModel>(SettingsSection);

    /// <summary>
    /// 儲存設定快照。
    /// </summary>
    public void SaveSettings(SettingsModel settings)
        => _iniRepository.SaveValues(SettingsSection, ResolveValues(settings));

    /// <summary>
    /// 解析啟動後要進入的首頁識別碼。
    /// </summary>
    public string ResolveStartupPageId()
    {
        var settings = QuerySettings();
        var lastVisited = settings.LastVisitedPageTag ?? string.Empty;

        return settings.StartPageIndex == 1 && !string.IsNullOrWhiteSpace(lastVisited)
            ? lastVisited
            : DefaultHomePageId;
    }

    /// <summary>
    /// 寫回最後瀏覽頁面識別碼。
    /// </summary>
    public void SaveLastVisitedPageId(string pageId)
    {
        var normalized = string.IsNullOrWhiteSpace(pageId) ? string.Empty : pageId;
        var existing = _iniRepository.QuerySnapshot<SettingsModel>(SettingsSection).LastVisitedPageTag ?? string.Empty;
        if (string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _iniRepository.SaveValues(SettingsSection, new Dictionary<string, string?>
        {
            [nameof(SettingsModel.LastVisitedPageTag)] = normalized,
        });
    }

    /// <summary>
    /// 依設定值解析閒置鎖定時間；未設定時回傳 <see langword="null"/>。
    /// </summary>
    public TimeSpan? QueryInactivityTimeout()
    {
        var index = QuerySettings().InactivityIndex;
        return InactivityTimeoutByIndex.TryGetValue(index, out var timeout) ? timeout : null;
    }

    /// <summary>
    /// 查詢設定頁顯示的工作區清單。
    /// </summary>
    public IReadOnlyList<SettingsWorkspaceItemModel> QueryWorkspaces()
    {
        var activeWorkspaceName = QueryActiveWorkspaceName();
        var root = _workspacesRepository.QueryWorkspacesDirectoryPath();

        return _workspacesRepository.QueryWorkspaceNames()
            .Select(workspaceName => new SettingsWorkspaceItemModel
            {
                Name = workspaceName,
                Description = Path.Combine(root, workspaceName),
                IsActive = string.Equals(workspaceName, activeWorkspaceName, StringComparison.OrdinalIgnoreCase),
            })
            .ToList();
    }

    /// <summary>
    /// 切換作用中的工作區。
    /// </summary>
    public void ActivateWorkspace(string workspaceName)
    {
        var resolvedWorkspaceName = _workspacesRepository.ResolveWorkspaceNameOrFallback(workspaceName);
        var workspaceDescription = string.IsNullOrWhiteSpace(resolvedWorkspaceName)
            ? string.Empty
            : _workspacesRepository.QueryWorkspaceDescription(resolvedWorkspaceName);

        _iniRepository.SaveValues(MainSection, new Dictionary<string, string?>
        {
            [nameof(MainModel.Workspace)] = resolvedWorkspaceName,
            [nameof(MainModel.WorkspaceDescription)] = workspaceDescription,
        });
    }

    /// <summary>
    /// 建立工作區並立即切換。
    /// </summary>
    public void CreateWorkspace(string workspaceName, string description)
    {
        _workspacesRepository.CreateWorkspace(workspaceName, description);
        ActivateWorkspace(workspaceName);
    }

    /// <summary>
    /// 刪除工作區；若刪除的是作用中工作區，會切回下一個有效工作區。
    /// </summary>
    public void DeleteWorkspace(string workspaceName)
    {
        var workspaceDirectoryPath = Path.Combine(_workspacesRepository.QueryWorkspacesDirectoryPath(), workspaceName);
        Directory.Delete(workspaceDirectoryPath, recursive: true);

        var remainingWorkspaceName = _workspacesRepository.QueryWorkspaceNames()
            .Where(name => !string.Equals(name, workspaceName, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault() ?? string.Empty;

        if (string.Equals(QueryActiveWorkspaceName(), workspaceName, StringComparison.OrdinalIgnoreCase))
        {
            var remainingWorkspaceDescription = string.IsNullOrWhiteSpace(remainingWorkspaceName)
                ? string.Empty
                : _workspacesRepository.QueryWorkspaceDescription(remainingWorkspaceName);

            _iniRepository.SaveValues(MainSection, new Dictionary<string, string?>
            {
                [nameof(MainModel.Workspace)] = remainingWorkspaceName,
                [nameof(MainModel.WorkspaceDescription)] = remainingWorkspaceDescription,
            });
        }
    }

    /// <summary>
    /// 查詢關於頁資訊。
    /// </summary>
    public SettingsAboutModel QueryAbout()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var versionText = assembly.GetName().Version?.ToString() ?? "-";
        var buildDateText = File.GetLastWriteTime(assembly.Location).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var licenseState = _applicationLicenseService.VerifyStoredLicense();

        return new SettingsAboutModel
        {
            VersionText = versionText,
            BuildDateText = buildDateText,
            LicenseExpiryText = licenseState.IsValid
                ? "已啟用（一般金鑰 / 無到期欄位）"
                : "未啟用",
        };
    }

    /// <summary>
    /// 匯出日誌為 ZIP 檔並回傳檔案路徑。
    /// </summary>
    public string ExportLogs(string exportDirectoryPath)
    {
        Directory.CreateDirectory(exportDirectoryPath);

        var logDirectoryPath = AppPathProvider.QueryLogsDirectory();
        var archiveFileName = $"Helix-Studio-Logs-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
        var archiveFilePath = Path.Combine(exportDirectoryPath, archiveFileName);
        File.Delete(archiveFilePath);

        using var archive = ZipFile.Open(archiveFilePath, ZipArchiveMode.Create);
        foreach (var filePath in Directory.EnumerateFiles(logDirectoryPath, "*", SearchOption.AllDirectories))
        {
            var entryName = Path.GetRelativePath(logDirectoryPath, filePath);
            archive.CreateEntryFromFile(filePath, entryName);
        }

        return archiveFilePath;
    }

    /// <summary>
    /// 開啟法律聲明檔。
    /// </summary>
    public void OpenLegalNotices(string languageCode)
    {
        var legalFilePath = AppPathProvider.QueryLegalNoticesFilePath(languageCode);
        Process.Start(new ProcessStartInfo
        {
            FileName = legalFilePath,
            UseShellExecute = true,
        });
    }

    private string QueryActiveWorkspaceName()
    {
        var configuredWorkspaceName = _iniRepository.QuerySnapshot<MainModel>(MainSection).Workspace ?? string.Empty;
        return _workspacesRepository.ResolveWorkspaceNameOrFallback(configuredWorkspaceName);
    }

    private static IReadOnlyDictionary<string, string?> ResolveValues<TSnapshot>(TSnapshot snapshot)
        where TSnapshot : class
        => snapshot
            .GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property is { CanRead: true })
            .ToDictionary(
                property => property.Name,
                property => (string?)Convert.ToString(property.GetValue(snapshot), CultureInfo.InvariantCulture),
                StringComparer.OrdinalIgnoreCase);
}
