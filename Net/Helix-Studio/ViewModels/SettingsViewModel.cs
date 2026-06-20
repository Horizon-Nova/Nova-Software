using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Helix_Studio.Data;
using Helix_Studio.Infrastructure;
using Helix_Studio.Models;
using Helix_Studio.Mvvm;
using Helix_Studio.Services;

namespace Helix_Studio.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly WorkspacesRepository _repo;
    private readonly AppSettingsService _appSettings;
    private readonly StartupSettingsService _startupSettings;
    private readonly ThemeService _themeService;
    private readonly BackupService _backup;
    private string _currentWorkspaceId;

    public override string PageKey => "settings";

    public ObservableCollection<WorkspaceInfo> Workspaces { get; }

    // 工作區管理
    public ICommand AddWorkspaceCommand { get; }
    public ICommand RemoveWorkspaceCommand { get; }
    public ICommand SwitchWorkspaceCommand { get; }
    public ICommand SaveNamesCommand { get; }
    public ICommand OpenHomeFolderCommand { get; }

    // FL-13: 日誌操作
    public ICommand OpenLogFolderCommand { get; }
    public ICommand BrowseLogFolderCommand { get; }

    // FL-18: 備份與還原
    public ICommand ExportBackupCommand { get; }
    public ICommand ImportBackupCommand { get; }

    public event Action<WorkspaceInfo>? SwitchRequested;

    public SettingsViewModel(WorkspacesRepository repo, IEnumerable<WorkspaceInfo> workspaces,
        string currentId, AppSettingsService appSettings, StartupSettingsService startupSettings,
        ThemeService themeService)
    {
        _repo = repo;
        _appSettings = appSettings;
        _startupSettings = startupSettings;
        _themeService = themeService;
        _backup = new BackupService(new InstalledToolsRepository(), repo);
        Workspaces = new ObservableCollection<WorkspaceInfo>(workspaces);
        _currentWorkspaceId = currentId;

        AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "—";

        AddWorkspaceCommand = new RelayCommand(_ => AddWorkspace());
        RemoveWorkspaceCommand = new RelayCommand(p => RemoveWorkspace(p as WorkspaceInfo));
        SwitchWorkspaceCommand = new RelayCommand(p => { if (p is WorkspaceInfo w) SwitchRequested?.Invoke(w); });
        SaveNamesCommand = new RelayCommand(_ => Save());
        OpenHomeFolderCommand = new RelayCommand(_ => OpenHomeFolder());
        OpenLogFolderCommand = new RelayCommand(_ => OpenLogFolder());
        BrowseLogFolderCommand = new RelayCommand(_ => BrowseLogFolder());
        ExportBackupCommand = new RelayCommand(_ => ExportBackup());
        ImportBackupCommand = new RelayCommand(_ => ImportBackup());
    }

    public string CurrentWorkspaceId
    {
        get => _currentWorkspaceId;
        set
        {
            if (SetProperty(ref _currentWorkspaceId, value))
                OnPropertyChanged(nameof(CurrentWorkspaceName));
        }
    }

    public string CurrentWorkspaceName
        => Workspaces.FirstOrDefault(w => w.Id == _currentWorkspaceId)?.Name ?? "—";

    public string AppVersion { get; }
    public string HomePath => AppPathProvider.Home;

    // FL-13: 日誌設定（全部即時儲存）
    public string LogOutputPath
    {
        get => _appSettings.Settings.LogOutputPath;
        set { _appSettings.Settings.LogOutputPath = value; _appSettings.Save(); OnPropertyChanged(); }
    }
    public string LogTimeFormat
    {
        get => _appSettings.Settings.LogTimeFormat;
        set { _appSettings.Settings.LogTimeFormat = value; _appSettings.Save(); OnPropertyChanged(); }
    }
    public bool LogMilliseconds
    {
        get => _appSettings.Settings.LogMilliseconds;
        set { _appSettings.Settings.LogMilliseconds = value; _appSettings.Save(); OnPropertyChanged(); }
    }
    public string LogFormat
    {
        get => _appSettings.Settings.LogFormat;
        set { _appSettings.Settings.LogFormat = value; _appSettings.Save(); OnPropertyChanged(); }
    }
    public bool LogCodesEnabled
    {
        get => _appSettings.Settings.LogCodesEnabled;
        set { _appSettings.Settings.LogCodesEnabled = value; _appSettings.Save(); OnPropertyChanged(); }
    }
    public int LogRetentionDays
    {
        get => _appSettings.Settings.LogRetentionDays;
        set { _appSettings.Settings.LogRetentionDays = Math.Max(0, value); _appSettings.Save(); OnPropertyChanged(); }
    }
    public string LogRetentionOption
    {
        get => _appSettings.Settings.LogRetentionDays.ToString();
        set
        {
            if (int.TryParse(value, out var days) && days != _appSettings.Settings.LogRetentionDays)
            {
                _appSettings.Settings.LogRetentionDays = days;
                _appSettings.Save();
                OnPropertyChanged();
            }
        }
    }

    // FL-25: 日誌編碼
    public string LogEncoding
    {
        get => _appSettings.Settings.LogEncoding;
        set { _appSettings.Settings.LogEncoding = value; _appSettings.Save(); OnPropertyChanged(); }
    }

    // FL-15: 工具清單排序
    public string ToolSortRule
    {
        get => _appSettings.Settings.ToolSortRule;
        set { _appSettings.Settings.ToolSortRule = value; _appSettings.Save(); OnPropertyChanged(); }
    }

    // FL-16: 應用程式啟動設定（全部即時儲存）
    public string DefaultWorkspaceId
    {
        get => _appSettings.Settings.DefaultWorkspaceId;
        set { _appSettings.Settings.DefaultWorkspaceId = value; _appSettings.Save(); OnPropertyChanged(); }
    }
    public string ToolStartupBehavior
    {
        get => _appSettings.Settings.ToolStartupBehavior;
        set { _appSettings.Settings.ToolStartupBehavior = value; _appSettings.Save(); OnPropertyChanged(); }
    }
    public bool StartWithWindows
    {
        get => _appSettings.Settings.StartWithWindows;
        set
        {
            _appSettings.Settings.StartWithWindows = value;
            _startupSettings.ApplyStartWithWindows(value);
            _appSettings.Save();
            OnPropertyChanged();
        }
    }
    public bool StartMinimized
    {
        get => _appSettings.Settings.StartMinimized;
        set { _appSettings.Settings.StartMinimized = value; _appSettings.Save(); OnPropertyChanged(); }
    }

    // FL-19: 資源警報（全部即時儲存）
    public bool CpuAlertEnabled
    {
        get => _appSettings.Settings.CpuAlertEnabled;
        set { _appSettings.Settings.CpuAlertEnabled = value; _appSettings.Save(); OnPropertyChanged(); }
    }
    public int CpuAlertThreshold
    {
        get => _appSettings.Settings.CpuAlertThreshold;
        set { _appSettings.Settings.CpuAlertThreshold = Math.Clamp(value, 1, 99); _appSettings.Save(); OnPropertyChanged(); }
    }
    public bool MemoryAlertEnabled
    {
        get => _appSettings.Settings.MemoryAlertEnabled;
        set { _appSettings.Settings.MemoryAlertEnabled = value; _appSettings.Save(); OnPropertyChanged(); }
    }
    public int MemoryAlertThreshold
    {
        get => _appSettings.Settings.MemoryAlertThreshold;
        set { _appSettings.Settings.MemoryAlertThreshold = Math.Clamp(value, 1, 99); _appSettings.Save(); OnPropertyChanged(); }
    }
    public bool ToolLevelAlertsEnabled
    {
        get => _appSettings.Settings.ToolLevelAlertsEnabled;
        set { _appSettings.Settings.ToolLevelAlertsEnabled = value; _appSettings.Save(); OnPropertyChanged(); }
    }

    // FL-20: 工作區切換行為
    public string WorkspaceSwitchBehavior
    {
        get => _appSettings.Settings.WorkspaceSwitchBehavior;
        set { _appSettings.Settings.WorkspaceSwitchBehavior = value; _appSettings.Save(); OnPropertyChanged(); }
    }

    // FL-22: 介面主題
    public string Theme
    {
        get => _appSettings.Settings.Theme;
        set
        {
            if (_appSettings.Settings.Theme == value) return;
            _appSettings.Settings.Theme = value;
            _themeService.Apply(value);
            _appSettings.Save();
            OnPropertyChanged();
        }
    }

    // FL-23: 字體縮放（double，供 ScaleTransform 綁定）
    public double FontScale => _appSettings.Settings.FontScale;

    // FL-23: 字體縮放選項（string，供 ComboBox Tag 綁定）
    public string FontScaleOption
    {
        get => _appSettings.Settings.FontScale.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture);
        set
        {
            if (!double.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var scale)
                || scale == _appSettings.Settings.FontScale) return;
            _appSettings.Settings.FontScale = scale;
            _appSettings.Save();
            OnPropertyChanged(nameof(FontScaleOption));
            OnPropertyChanged(nameof(FontScale));
        }
    }

    // FL-24: 介面語言
    public string Language
    {
        get => _appSettings.Settings.Language;
        set { _appSettings.Settings.Language = value; _appSettings.Save(); OnPropertyChanged(); }
    }

    private void AddWorkspace()
    {
        var name = Views.Dialogs.AddWorkspaceDialog.Show(Application.Current?.MainWindow);
        if (name is null) return;
        var id = "ws-" + Guid.NewGuid().ToString("N")[..8];
        Workspaces.Add(new WorkspaceInfo { Id = id, Name = name });
        Save();
    }

    private void RemoveWorkspace(WorkspaceInfo? ws)
    {
        if (ws is null || ws.Id == _currentWorkspaceId || Workspaces.Count <= 1) return;
        var dir = AppPathProvider.WorkspaceDir(ws.Id);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        Workspaces.Remove(ws);
        Save();
    }

    private void OpenHomeFolder()
    {
        Directory.CreateDirectory(AppPathProvider.Home);
        Process.Start(new ProcessStartInfo { FileName = AppPathProvider.Home, UseShellExecute = true });
    }

    private void OpenLogFolder()
    {
        var path = string.IsNullOrEmpty(_appSettings.Settings.LogOutputPath)
            ? AppPathProvider.Home
            : _appSettings.Settings.LogOutputPath;
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private void BrowseLogFolder()
    {
        var dialog = new OpenFolderDialog { Title = "選擇日誌輸出資料夾" };
        if (dialog.ShowDialog() == true)
            LogOutputPath = dialog.FolderName;
    }

    public void Save()
    {
        _repo.Save(new WorkspacesFile
        {
            Workspaces = Workspaces.ToList(),
            CurrentId = _currentWorkspaceId,
        });
        OnPropertyChanged(nameof(CurrentWorkspaceName));
    }

    private void ExportBackup()
    {
        var dialog = new SaveFileDialog
        {
            Title = "匯出備份",
            Filter = "Helix 備份 (*.hbak)|*.hbak",
            FileName = $"helix-backup-{DateTime.Now:yyyyMMdd}.hbak",
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var ids = Workspaces.Select(w => w.Id).ToList();
            _backup.Export(dialog.FileName, ids);
            MessageBox.Show($"備份已儲存至：{dialog.FileName}", "備份成功",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"匯出失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportBackup()
    {
        var dialog = new OpenFileDialog
        {
            Title = "從備份還原",
            Filter = "Helix 備份 (*.hbak)|*.hbak",
        };
        if (dialog.ShowDialog() != true) return;

        var result = _backup.Validate(dialog.FileName);
        if (!result.IsValid)
        {
            var msg = result.Error switch
            {
                "invalid_zip" => "所選檔案不是有效的 Helix 備份檔",
                "missing_manifest" or "invalid_manifest" or "missing_workspace_data" => "備份檔案內容損毀或格式不正確",
                "incompatible_version" => "此備份由更新版本的 Helix Studio 建立，請升級後再還原",
                _ => "備份檔案無效",
            };
            MessageBox.Show(msg, "還原失敗", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var wsNames = string.Join("、", result.Manifest!.WorkspaceIds);
        var confirm = MessageBox.Show(
            $"備份包含 {result.Manifest.WorkspaceIds.Count} 個工作區：{wsNames}\n\n" +
            "還原不會自動安裝工具執行檔，完成後需手動安裝。\n\n確定要還原嗎？",
            "確認還原", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        try
        {
            _backup.Restore(dialog.FileName, overwriteExisting: true);
            MessageBox.Show("工作區設定已還原，請重新啟動應用程式使設定生效。", "還原成功",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"還原失敗，現有設定未被修改。\n{ex.Message}", "錯誤",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
