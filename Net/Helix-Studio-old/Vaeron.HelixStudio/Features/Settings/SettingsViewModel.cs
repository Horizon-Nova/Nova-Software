using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vaeron.HelixStudio.Features.EventLog;
using Vaeron.HelixStudio.Features.EventLog.Contracts;
using Vaeron.HelixStudio.Features.Settings.Contracts;
using Vaeron.HelixStudio.Features.WorkspaceManagement;
using Vaeron.HelixStudio.Features.WorkspaceManagement.Contracts;
using Vaeron.HelixStudio.Themes;
using Vaeron.Platform;
using Vaeron.Platform.AppRuntime;

namespace Vaeron.HelixStudio.Features.Settings;

public sealed record CategoryDef(string Key, string Label, string Keywords);

/// <summary>畫面用的工作區列（附事件數）</summary>
public sealed record WorkspaceRow(WorkspaceSnapshot Workspace, int EventCount);

public sealed partial class SettingsViewModel(
    SettingsDomain domain,
    WorkspaceManagementFacade workspaceFacade,
    EventLogFacade eventLogFacade,
    ThemePackService themePacks,
    NotificationRuntime notificationRuntime) : ViewModelBase
{
    /// <summary>組件檔案寫入時間（建置日期）</summary>
    private static readonly DateTimeOffset AssemblyBuildTime =
        System.IO.File.GetLastWriteTime(Assembly.GetExecutingAssembly().Location);

    public ObservableCollection<WorkspaceRow> Workspaces { get; } = [];

    public string[] StartupPageOptions { get; } = ["apps", "library", "events", "settings"];
    public EventSeverity[] LogLevelOptions { get; } =
        [EventSeverity.Info, EventSeverity.Warning, EventSeverity.Error, EventSeverity.None];
    public int[] RetentionDaysOptions { get; } = [3, 7, 14, 30, -1];
    public int[] UiScaleOptions { get; } = [50, 75, 90, 100, 110, 125, 150, 175, 200];
    public string[] LanguageOptions { get; } = ["zh-TW", "zh-CN", "en"];
    public int[] IdleLockMinutesOptions { get; } = [0, 5, 15, 30, 60];
    public string[] PowerModeOptions { get; } = ["省電", "平衡", "高效能"];
    public int[] SleepMinutesOptions { get; } = [1, 2, 3, 5, 10, 15, 20, 30, 45, 60, 120, 180, 240, 300, 0];
    public string[] PowerButtonActionOptions { get; } = ["不執行動作", "睡眠", "休眠", "關機", "顯示關閉選單"];
    public string[] EncodingOptions { get; } = ["UTF-8", "UTF-16", "UTF-32", "Big5", "GBK", "Shift-JIS", "ANSI"];
    public string[] TimeZoneOptions { get; } =
    [
        "本地時間（系統時區）", "UTC±00:00",
        "UTC−12:00", "UTC−11:00", "UTC−10:00", "UTC−09:30", "UTC−09:00", "UTC−08:00（洛杉磯）",
        "UTC−07:00（丹佛）", "UTC−06:00（芝加哥）", "UTC−05:00（紐約）", "UTC−04:00", "UTC−03:30",
        "UTC−03:00（聖保羅）", "UTC−02:00", "UTC−01:00",
        "UTC+01:00（柏林）", "UTC+02:00（開羅）", "UTC+03:00（莫斯科）", "UTC+03:30（德黑蘭）",
        "UTC+04:00（杜拜）", "UTC+04:30（喀布爾）", "UTC+05:00（卡拉奇）", "UTC+05:30（新德里）",
        "UTC+05:45（加德滿都）", "UTC+06:00（達卡）", "UTC+06:30（仰光）", "UTC+07:00（曼谷）",
        "UTC+08:00（台北／香港／新加坡）", "UTC+08:45", "UTC+09:00（東京／首爾）", "UTC+09:30（阿德雷德）",
        "UTC+10:00（雪梨）", "UTC+10:30", "UTC+11:00", "UTC+12:00（奧克蘭）", "UTC+12:45",
        "UTC+13:00", "UTC+14:00",
    ];

    public string VersionText { get; } = "1.0.0";
    public string BuildDateText { get; } = AssemblyBuildTime.ToString("yyyy-MM-dd");
    public string PlatformText { get; } = ".NET 8 · WPF";

    public CategoryDef[] Categories { get; } =
    [
        new("workspace", "工作區（產線）管理", "工作區 產線 管理 新增 刪除 切換"),
        new("appearance", "外觀", "外觀 深色模式 精簡密度 介面縮放 強調色 主題"),
        new("power", "電源", "電源 省電 平衡 高效能 睡眠 螢幕逾時 休眠 快速啟動"),
        new("general", "一般", "一般 側邊欄 開機 啟動 語言 閒置鎖定"),
        new("notifications", "通知", "通知 未讀 徽章"),
        new("log", "日誌", "日誌 最低記錄等級 記錄保留期限 輸出位置 編碼 utf-8 big5 gbk ansi shift-jis 時間 utc 時區 匯出事件記錄"),
        new("about", "關於", "關於 版本 建置日期 平台 授權 到期日 檢查更新 工作區 id"),
        new("help", "使用說明", "使用說明 操作手冊 faq 常見問題 鍵盤快速鍵 changelog 系統需求 意見回饋 錯誤回報 技術支援"),
        new("legal", "法律", "法律 隱私權政策 服務條款 使用條款 第三方軟體授權 資料保留 稽核政策"),
    ];

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string? _selectedCategoryKey;

    [ObservableProperty] private int _uiScalePercent = 100;
    [ObservableProperty] private bool _isCompactDensity;

    /// <summary>可選主題：內建預設 + 主題資料夾裡掃到的包，不是寫死的選項清單</summary>
    public ObservableCollection<ThemePack> ThemePacks { get; } = [];
    [ObservableProperty] private string _themePackId = ThemePack.DefaultId;
    [ObservableProperty] private bool _backgroundAnimationEnabled = true;

    [ObservableProperty] private bool _sidebarCollapsedByDefault;
    [ObservableProperty] private string _startupPage = "apps";
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _showEventBadge = true;

    [ObservableProperty] private EventSeverity _minLogLevel = EventSeverity.Info;
    [ObservableProperty] private int _retentionDays = 30;

    [ObservableProperty] private bool _isAddingWorkspace;
    [ObservableProperty] private string _newWorkspaceName = "";
    [ObservableProperty] private string _newWorkspaceDescription = "";

    [ObservableProperty] private WorkspaceSnapshot? _editingWorkspace;
    [ObservableProperty] private string _editWorkspaceName = "";
    [ObservableProperty] private string _editWorkspaceDescription = "";

    [ObservableProperty] private WorkspaceSnapshot? _deleteTarget;
    [ObservableProperty] private string _deleteConfirmText = "";

    // 僅供展示欄位，不落地
    [ObservableProperty] private string _language = "zh-TW";
    [ObservableProperty] private int _idleLockMinutes;
    [ObservableProperty] private string _powerMode = "平衡";
    [ObservableProperty] private int _acSleepMinutes = 30;
    [ObservableProperty] private int _batterySleepMinutes = 15;
    [ObservableProperty] private int _screenTimeoutMinutes = 10;
    [ObservableProperty] private bool _hibernateEnabled = true;
    [ObservableProperty] private bool _fastStartupEnabled = true;
    [ObservableProperty] private string _powerButtonAction = "睡眠";
    [ObservableProperty] private string _lidCloseAction = "睡眠";
    [ObservableProperty] private string _encoding = "UTF-8";
    [ObservableProperty] private string _timeZone = "UTC+08:00（台北／香港／新加坡）";
    [ObservableProperty] private string _exportPathDisplay = ExportPathPlaceholder;

    [ObservableProperty] private bool _isCheckingUpdate;
    [ObservableProperty] private int? _activeWorkspaceId;

    #region 分類搜尋

    partial void OnSearchTextChanged(string value) => RaiseCategoryVisibilityChanged();

    private bool IsCategoryVisible(string key)
    {
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        var category = Categories.FirstOrDefault(c => c.Key == key);
        return category is not null && category.Keywords.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsWorkspaceCategoryVisible => IsCategoryVisible("workspace");
    public bool IsAppearanceCategoryVisible => IsCategoryVisible("appearance");
    public bool IsPowerCategoryVisible => IsCategoryVisible("power");
    public bool IsGeneralCategoryVisible => IsCategoryVisible("general");
    public bool IsNotificationsCategoryVisible => IsCategoryVisible("notifications");
    public bool IsLogCategoryVisible => IsCategoryVisible("log");
    public bool IsAboutCategoryVisible => IsCategoryVisible("about");
    public bool IsHelpCategoryVisible => IsCategoryVisible("help");
    public bool IsLegalCategoryVisible => IsCategoryVisible("legal");

    public bool HasNoCategoryMatch => Categories.All(c => !IsCategoryVisible(c.Key));

    private void RaiseCategoryVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsWorkspaceCategoryVisible));
        OnPropertyChanged(nameof(IsAppearanceCategoryVisible));
        OnPropertyChanged(nameof(IsPowerCategoryVisible));
        OnPropertyChanged(nameof(IsGeneralCategoryVisible));
        OnPropertyChanged(nameof(IsNotificationsCategoryVisible));
        OnPropertyChanged(nameof(IsLogCategoryVisible));
        OnPropertyChanged(nameof(IsAboutCategoryVisible));
        OnPropertyChanged(nameof(IsHelpCategoryVisible));
        OnPropertyChanged(nameof(IsLegalCategoryVisible));
        OnPropertyChanged(nameof(HasNoCategoryMatch));
    }

    /// <summary>切換分類（先清空搜尋字）</summary>
    [RelayCommand]
    private void ApplySelectCategory(string key)
    {
        SearchText = "";
        SelectedCategoryKey = key;
    }

    #endregion

    #region 生命週期

    public void ApplyViewLoaded()
    {
        domain.Changed += OnChanged;
        workspaceFacade.WorkspaceChanged += OnChanged;
        eventLogFacade.EventsChanged += OnChanged;
        RunSafe(ApplySnapshotAsync);
    }

    public void ApplyViewUnloaded()
    {
        domain.Changed -= OnChanged;
        workspaceFacade.WorkspaceChanged -= OnChanged;
        eventLogFacade.EventsChanged -= OnChanged;
    }

    private void OnChanged() => RunSafe(ApplySnapshotAsync);

    /// <summary>載入快照期間避免 On*Changed 誤觸發寫回</summary>
    private bool _isApplyingSnapshot;
    private string? _exportPath;

    private const string ExportPathPlaceholder = "（尚未設定，匯出時另外選擇檔案位置）";

    private async Task ApplySnapshotAsync()
    {
        var settings = await domain.LoadSettingsAsync();
        var workspaces = await workspaceFacade.LoadWorkspaceListAsync();
        var logSettings = await eventLogFacade.LoadLogSettingsAsync();
        var active = await workspaceFacade.LoadActiveWorkspaceAsync();

        var rows = new List<WorkspaceRow>();
        foreach (var w in workspaces)
        {
            var count = (await eventLogFacade.LoadEventListAsync(w.Id)).Count;
            rows.Add(new WorkspaceRow(w, count));
        }

        PostToUi(() =>
        {
            _isApplyingSnapshot = true;
            ThemePacks.Clear();
            foreach (var p in themePacks.LoadAvailablePacks())
                ThemePacks.Add(p);
            ThemePackId = ThemePacks.Any(p => p.Id == settings.ThemePackId) ? settings.ThemePackId : ThemePack.DefaultId;
            BackgroundAnimationEnabled = settings.BackgroundAnimationEnabled;
            UiScalePercent = settings.UiScalePercent;
            IsCompactDensity = settings.DataDensity == DataDensity.Compact;
            SidebarCollapsedByDefault = settings.SidebarCollapsedByDefault;
            StartupPage = settings.StartupPage;
            StartWithWindows = settings.StartWithWindows;
            ShowEventBadge = settings.ShowEventBadge;
            MinLogLevel = logSettings.MinLevel;
            RetentionDays = logSettings.RetentionDays;
            _exportPath = logSettings.ExportPath;
            ExportPathDisplay = _exportPath ?? ExportPathPlaceholder;
            ActiveWorkspaceId = active?.Id;

            Workspaces.Clear();
            foreach (var r in rows)
                Workspaces.Add(r);
            _isApplyingSnapshot = false;
        });
    }

    #endregion

    #region 外觀

    /// <summary>資料密度按鈕群組選取鍵</summary>
    public string DensityKey => IsCompactDensity ? "compact" : "comfortable";

    partial void OnIsCompactDensityChanged(bool value)
    {
        OnPropertyChanged(nameof(DensityKey));
        if (!_isApplyingSnapshot) RunSafe(SaveAsync);
    }

    partial void OnUiScalePercentChanged(int value) { if (!_isApplyingSnapshot) RunSafe(SaveAsync); }
    partial void OnBackgroundAnimationEnabledChanged(bool value) { if (!_isApplyingSnapshot) RunSafe(SaveAsync); }

    [RelayCommand]
    private void ApplySelectDensity(string key) => IsCompactDensity = key == "compact";

    [RelayCommand]
    private async Task ApplySelectThemePackAsync(ThemePack pack)
    {
        var (success, message) = themePacks.Apply(pack);
        notificationRuntime.ApplyShowResult(success, message);
        if (!success) return;

        ThemePackId = pack.Id;
        await SaveAsync();
    }

    /// <summary>開啟主題資料夾，讓使用者直接把主題包放進去</summary>
    [RelayCommand]
    private void ApplyOpenThemeFolder()
    {
        var folder = themePacks.ThemeDirectory;
        System.IO.Directory.CreateDirectory(folder);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
    }

    /// <summary>重新掃描主題資料夾</summary>
    [RelayCommand]
    private void ApplyRefreshThemePacks()
    {
        var current = ThemePackId;
        ThemePacks.Clear();
        foreach (var p in themePacks.LoadAvailablePacks())
            ThemePacks.Add(p);
        ThemePackId = ThemePacks.Any(p => p.Id == current) ? current : ThemePack.DefaultId;
    }

    [RelayCommand]
    private async Task ApplyResetAppearanceAsync()
    {
        _isApplyingSnapshot = true;
        UiScalePercent = 100;
        IsCompactDensity = false;
        _isApplyingSnapshot = false;

        await SaveAsync();
    }

    #endregion

    #region 電源（僅供展示）

    [RelayCommand]
    private void ApplySelectPowerMode(string mode) => PowerMode = mode;

    #endregion

    #region 一般／通知

    partial void OnSidebarCollapsedByDefaultChanged(bool value) { if (!_isApplyingSnapshot) RunSafe(SaveAsync); }
    partial void OnStartWithWindowsChanged(bool value) { if (!_isApplyingSnapshot) RunSafe(SaveAsync); }
    partial void OnShowEventBadgeChanged(bool value) { if (!_isApplyingSnapshot) RunSafe(SaveAsync); }

    private async Task SaveAsync()
    {
        var (success, message) = await domain.UpdateSettingsAsync(new AppSettingsSnapshot
        {
            ThemePackId = ThemePackId,
            BackgroundAnimationEnabled = BackgroundAnimationEnabled,
            UiScalePercent = UiScalePercent,
            DataDensity = IsCompactDensity ? DataDensity.Compact : DataDensity.Comfortable,
            SidebarCollapsedByDefault = SidebarCollapsedByDefault,
            StartWithWindows = StartWithWindows,
            StartupPage = StartupPage,
            ShowEventBadge = ShowEventBadge,
        });
        notificationRuntime.ApplyShowResult(success, message);
    }

    #endregion

    #region 日誌

    partial void OnMinLogLevelChanged(EventSeverity value) { if (!_isApplyingSnapshot) RunSafe(ApplySaveLogSettingsAsync); }
    partial void OnRetentionDaysChanged(int value) { if (!_isApplyingSnapshot) RunSafe(ApplySaveLogSettingsAsync); }

    private async Task ApplySaveLogSettingsAsync()
    {
        var (success, message) = await eventLogFacade.UpdateLogSettingsAsync(new LogSettingsSnapshot
        {
            MinLevel = MinLogLevel,
            RetentionDays = RetentionDays,
            ExportPath = _exportPath,
        });
        notificationRuntime.ApplyShowResult(success, message);
    }

    [RelayCommand]
    private async Task ApplyChooseExportPathAsync()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "選擇匯出資料夾" };
        if (_exportPath is not null) dialog.InitialDirectory = _exportPath;
        if (dialog.ShowDialog() != true) return;

        _exportPath = dialog.FolderName;
        ExportPathDisplay = _exportPath;
        await ApplySaveLogSettingsAsync();
    }

    [RelayCommand]
    private async Task ApplyExportEventsAsync()
    {
        var active = await workspaceFacade.LoadActiveWorkspaceAsync();
        if (active is null) return;

        var events = await eventLogFacade.LoadEventListAsync(active.Id);
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"events-{active.Name}-{DateTimeOffset.Now:yyyyMMddHHmmss}.json",
            Filter = "JSON (*.json)|*.json",
        };
        if (_exportPath is not null) dialog.InitialDirectory = _exportPath;
        if (dialog.ShowDialog() != true) return;

        var json = System.Text.Json.JsonSerializer.Serialize(events,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        System.IO.File.WriteAllText(dialog.FileName, json);
        notificationRuntime.ApplyShowResult(true, "已匯出");
    }

    #endregion

    #region 關於

    [RelayCommand]
    private async Task ApplyCheckUpdateAsync()
    {
        IsCheckingUpdate = true;
        await Task.Delay(900);
        IsCheckingUpdate = false;
        notificationRuntime.ApplyShowResult(false, "更新檢查功能尚未串接後端服務");
    }

    #endregion

    #region 工作區管理

    [RelayCommand]
    private void ApplyToggleAddWorkspace()
    {
        IsAddingWorkspace = !IsAddingWorkspace;
        NewWorkspaceName = "";
        NewWorkspaceDescription = "";
    }

    [RelayCommand]
    private async Task ApplyAddWorkspaceAsync()
    {
        var (success, message) = await workspaceFacade.CreateWorkspaceAsync(new CreateWorkspaceRequest
        {
            Name = NewWorkspaceName,
            Description = NewWorkspaceDescription,
        });
        notificationRuntime.ApplyShowResult(success, message);
        if (success)
        {
            NewWorkspaceName = "";
            NewWorkspaceDescription = "";
            IsAddingWorkspace = false;
        }
    }

    [RelayCommand]
    private void ApplyStartEditWorkspace(WorkspaceSnapshot workspace)
    {
        EditingWorkspace = workspace;
        EditWorkspaceName = workspace.Name;
        EditWorkspaceDescription = workspace.Description ?? "";
    }

    [RelayCommand]
    private void ApplyCancelEditWorkspace() => EditingWorkspace = null;

    [RelayCommand]
    private async Task ApplySaveEditWorkspaceAsync()
    {
        if (EditingWorkspace is null) return;
        var (success, message) = await workspaceFacade.RenameWorkspaceAsync(new RenameWorkspaceRequest
        {
            Id = EditingWorkspace.Id,
            Name = EditWorkspaceName,
            Description = EditWorkspaceDescription,
        });
        notificationRuntime.ApplyShowResult(success, message);
        if (success) EditingWorkspace = null;
    }

    /// <summary>請求刪除工作區（需輸入完整名稱確認）</summary>
    [RelayCommand]
    private void ApplyRequestDeleteWorkspace(WorkspaceSnapshot workspace)
    {
        DeleteTarget = workspace;
        DeleteConfirmText = "";
    }

    [RelayCommand]
    private void ApplyCancelDeleteWorkspace()
    {
        DeleteTarget = null;
        DeleteConfirmText = "";
    }

    [RelayCommand]
    private async Task ApplyConfirmDeleteWorkspaceAsync()
    {
        if (DeleteTarget is null || DeleteConfirmText != DeleteTarget.Name) return;

        var (success, message) = await workspaceFacade.DeleteWorkspaceAsync(DeleteTarget.Id);
        notificationRuntime.ApplyShowResult(success, message);
        if (success)
        {
            DeleteTarget = null;
            DeleteConfirmText = "";
        }
    }

    [RelayCommand]
    private async Task ApplySwitchWorkspaceAsync(WorkspaceSnapshot workspace)
    {
        var (success, message) = await workspaceFacade.SwitchActiveWorkspaceAsync(workspace.Id);
        notificationRuntime.ApplyShowResult(success, message);
    }

    #endregion
}
