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
    private string _activeSection = string.Empty;

    public override string PageKey => "settings";

    public ObservableCollection<WorkspaceInfo> Workspaces { get; }

    // 工作區管理
    public ICommand AddWorkspaceCommand { get; }
    public ICommand RemoveWorkspaceCommand { get; }
    public ICommand SwitchWorkspaceCommand { get; }
    public ICommand SaveNamesCommand { get; }
    public ICommand OpenHomeFolderCommand { get; }

    // FL-08 N-005: 日誌操作
    public ICommand OpenLogFolderCommand { get; }
    public ICommand BrowseLogFolderCommand { get; }

    // FL-02 N-007: 備份與還原
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

    public string ActiveSection
    {
        get => _activeSection;
        set => SetProperty(ref _activeSection, value);
    }

    // FL-08 N-005: 日誌設定（全部即時儲存）
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

    // FL-08 N-005: 日誌編碼
    public string LogEncoding
    {
        get => _appSettings.Settings.LogEncoding;
        set { _appSettings.Settings.LogEncoding = value; _appSettings.Save(); OnPropertyChanged(); }
    }

    // FL-03 N-007: 工具清單排序
    public string ToolSortRule
    {
        get => _appSettings.Settings.ToolSortRule;
        set { _appSettings.Settings.ToolSortRule = value; _appSettings.Save(); OnPropertyChanged(); }
    }

    // FL-08 N-002: 應用程式啟動設定（全部即時儲存）
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
    public bool CloseToTray
    {
        get => _appSettings.Settings.CloseToTray;
        set { _appSettings.Settings.CloseToTray = value; _appSettings.Save(); OnPropertyChanged(); }
    }

    // FL-08 N-006: 資源警報（全部即時儲存）
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

    // FL-08 N-004 / FL-02 N-003: 工作區切換行為
    public string WorkspaceSwitchBehavior
    {
        get => _appSettings.Settings.WorkspaceSwitchBehavior;
        set { _appSettings.Settings.WorkspaceSwitchBehavior = value; _appSettings.Save(); OnPropertyChanged(); }
    }

    // FL-08 N-003: 介面主題
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

    // FL-08 N-003: 字體縮放（double，供 ScaleTransform 綁定）
    public double FontScale => _appSettings.Settings.FontScale;

    // FL-08 N-003: 字體縮放選項（string，供 ComboBox Tag 綁定）
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

    // FL-08 N-001: 介面語言（即時切換，無需重新啟動）
    public string Language
    {
        get => _appSettings.Settings.Language;
        set
        {
            if (_appSettings.Settings.Language == value) return;
            _appSettings.Settings.Language = value;
            LocalizationService.Instance.SetLanguage(value);
            _appSettings.Save();
            OnPropertyChanged();
        }
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
        var dialog = new OpenFolderDialog { Title = LocalizationService.Instance.GetString("L.Dialog.BrowseLogFolder") };
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
        var loc = LocalizationService.Instance;
        var dialog = new SaveFileDialog
        {
            Title = loc.GetString("L.Settings.About.BackupExport"),
            Filter = loc.GetString("L.Backup.Filter"),
            FileName = $"helix-backup-{DateTime.Now:yyyyMMdd}.hbak",
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var ids = Workspaces.Select(w => w.Id).ToList();
            _backup.Export(dialog.FileName, ids);
            MessageBox.Show(loc.Format("L.Backup.ExportSuccessMsg", dialog.FileName),
                loc.GetString("L.Backup.ExportSuccessTitle"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(loc.Format("L.Backup.ExportFailMsg", ex.Message),
                loc.GetString("L.Common.ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportBackup()
    {
        var loc = LocalizationService.Instance;
        var dialog = new OpenFileDialog
        {
            Title = loc.GetString("L.Backup.ImportTitle"),
            Filter = loc.GetString("L.Backup.Filter"),
        };
        if (dialog.ShowDialog() != true) return;

        var result = _backup.Validate(dialog.FileName);
        if (!result.IsValid)
        {
            var msgKey = result.Error switch
            {
                "invalid_zip" => "L.Backup.ValidateInvalidZip",
                "missing_manifest" or "invalid_manifest" or "missing_workspace_data" => "L.Backup.ValidateCorrupt",
                "incompatible_version" => "L.Backup.ValidateIncompatible",
                _ => "L.Backup.ValidateGeneric",
            };
            MessageBox.Show(loc.GetString(msgKey), loc.GetString("L.Backup.ImportFailTitle"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var wsNames = string.Join("、", result.Manifest!.WorkspaceIds);
        var confirm = MessageBox.Show(
            loc.Format("L.Backup.ConfirmMsg", result.Manifest.WorkspaceIds.Count, wsNames),
            loc.GetString("L.Backup.ConfirmTitle"), MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        try
        {
            _backup.Restore(dialog.FileName, overwriteExisting: true);
            MessageBox.Show(loc.GetString("L.Backup.ImportSuccessMsg"),
                loc.GetString("L.Backup.ImportSuccessTitle"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(loc.Format("L.Backup.ImportFailMsg", ex.Message),
                loc.GetString("L.Common.ErrorTitle"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
