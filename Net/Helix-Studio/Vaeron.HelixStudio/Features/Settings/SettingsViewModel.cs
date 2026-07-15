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

/// <summary>畫面用的工作區列，附帶事件數——事件數不屬於 WorkspaceSnapshot 的資料形狀
/// （contracts.md：Snapshot 只承載該 Feature 自己的資料），只在本畫面組裝。</summary>
public sealed record WorkspaceRow(WorkspaceSnapshot Workspace, int EventCount);

public sealed partial class SettingsViewModel(
    SettingsDomain domain,
    WorkspaceManagementFacade workspaceFacade,
    EventLogFacade eventLogFacade,
    NotificationRuntime notificationRuntime) : ViewModelBase
{
    /// <summary>建置時間來源改用組件檔案的實際寫入時間，不是每次啟動抓 DateTimeOffset.Now
    /// 冒充建置日期——同一次建置產出的 dll，這個時間戳記固定不變。</summary>
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

    [ObservableProperty] private bool _isDarkMode;
    [ObservableProperty] private double _accentHue = AccentColorService.DefaultHue;
    [ObservableProperty] private double _accentChroma = AccentColorService.DefaultChroma;
    [ObservableProperty] private int _uiScalePercent = 100;
    [ObservableProperty] private bool _isCompactDensity;

    [ObservableProperty] private bool _sidebarCollapsedByDefault;
    [ObservableProperty] private string _startupPage = "apps";
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _showEventBadge = true;

    [ObservableProperty] private EventSeverity _minLogLevel = EventSeverity.Info;
    [ObservableProperty] private int _retentionDays = 30;

    [ObservableProperty] private string _newWorkspaceName = "";
    [ObservableProperty] private string _newWorkspaceDescription = "";

    [ObservableProperty] private WorkspaceSnapshot? _editingWorkspace;
    [ObservableProperty] private string _editWorkspaceName = "";
    [ObservableProperty] private string _editWorkspaceDescription = "";

    [ObservableProperty] private WorkspaceSnapshot? _deleteTarget;
    [ObservableProperty] private string _deleteConfirmText = "";

    // 一般／電源／日誌的「僅供展示」欄位——沒有對應的持久化資料形狀，值只存在畫面執行期間，
    // 理由同 blueprint 對這些欄位的定位：展示互動可能性，不接真的 OS API 或後端服務。
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
    [ObservableProperty] private string _timeZone = "UTC+08:00 台北";
    [ObservableProperty] private string _exportPathDisplay = "（尚未設定，匯出時另外選擇檔案位置）";

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

    /// <summary>切分類前先清空搜尋字——搜尋條件可能把目標分類的 Border 判成 Collapsed，
    /// 這時點側邊欄按鈕會呼叫 BringIntoView 在一個看不見的元素上，畫面上像是點了沒反應。
    /// blueprint 的 selectCategory 本來就會先清 searchQuery 再捲動，這裡照做。</summary>
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

    /// <summary>從資料庫載入設定、把值寫進畫面屬性的這段期間，各屬性的 On*Changed 自動套用
    /// 處理常式（見下方「外觀」region）不能跟著誤觸發寫回資料庫——不然開畫面那一刻就會
    /// 把剛讀出來的值又寫一次，多餘且在 ApplySnapshotAsync 還沒跑完時寫入的還可能是半套資料。
    /// 這個旗標只在 PostToUi 那個同步區塊內為 true，界線清楚。</summary>
    private bool _isApplyingSnapshot;

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
            IsDarkMode = settings.ThemeMode == ThemeMode.Dark;
            AccentHue = settings.AccentHue;
            AccentChroma = settings.AccentChroma;
            UiScalePercent = settings.UiScalePercent;
            IsCompactDensity = settings.DataDensity == DataDensity.Compact;
            SidebarCollapsedByDefault = settings.SidebarCollapsedByDefault;
            StartupPage = settings.StartupPage;
            StartWithWindows = settings.StartWithWindows;
            ShowEventBadge = settings.ShowEventBadge;
            MinLogLevel = logSettings.MinLevel;
            RetentionDays = logSettings.RetentionDays;
            ActiveWorkspaceId = active?.Id;

            Workspaces.Clear();
            foreach (var r in rows)
                Workspaces.Add(r);
            _isApplyingSnapshot = false;
        });

        ApplyAccentToResources();
    }

    private void ApplyAccentToResources()
        => AccentColorService.Apply(System.Windows.Application.Current.Resources, AccentHue, AccentChroma, IsDarkMode);

    #endregion

    #region 外觀

    /// <summary>blueprint 這個畫面的外觀控件沒有「套用」這個步驟——每個控件一改就立刻生效
    /// 並存檔（onScaleChange／主題色卡 onClick／密度選取都直接呼叫對應 callback），這裡
    /// 用 On*Changed 局部方法對齊同樣的即時套用行為，不額外做設計稿沒有的「套用」按鈕。
    /// _isApplyingSnapshot 為 true 時（正在從資料庫載入）不觸發，見 ApplySnapshotAsync。</summary>
    partial void OnIsDarkModeChanged(bool value) { if (!_isApplyingSnapshot) RunSafe(ApplySaveAppearanceAsync); }
    partial void OnIsCompactDensityChanged(bool value) { if (!_isApplyingSnapshot) RunSafe(ApplySaveAppearanceAsync); }
    partial void OnUiScalePercentChanged(int value) { if (!_isApplyingSnapshot) RunSafe(ApplySaveAppearanceAsync); }
    partial void OnAccentHueChanged(double value) { if (!_isApplyingSnapshot) RunSafe(ApplySaveAppearanceAsync); }

    private async Task ApplySaveAppearanceAsync()
    {
        ApplyAccentToResources();
        SwapThemeDictionary(IsDarkMode);
        await SaveAsync();
    }

    private static void SwapThemeDictionary(bool isDark)
    {
        var app = System.Windows.Application.Current;
        var target = isDark ? "Themes/Tokens.Dark.xaml" : "Themes/Tokens.Light.xaml";
        var existing = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source?.OriginalString.Contains("Themes/Tokens.") == true);
        var newDict = new System.Windows.ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Vaeron.HelixStudio;component/{target}"),
        };
        if (existing is not null)
        {
            var index = app.Resources.MergedDictionaries.IndexOf(existing);
            app.Resources.MergedDictionaries[index] = newDict;
        }
        else
        {
            app.Resources.MergedDictionaries.Add(newDict);
        }
    }

    [RelayCommand]
    private void ApplySelectTheme(string mode) => IsDarkMode = mode == "dark";

    [RelayCommand]
    private void ApplyResetAppearance()
    {
        IsDarkMode = false;
        AccentHue = AccentColorService.DefaultHue;
        AccentChroma = AccentColorService.DefaultChroma;
        UiScalePercent = 100;
        IsCompactDensity = false;
    }

    #endregion

    #region 一般／通知

    [RelayCommand]
    private async Task ApplySaveGeneralAsync() => await SaveAsync();

    private async Task SaveAsync()
    {
        var (success, message) = await domain.UpdateSettingsAsync(new AppSettingsSnapshot
        {
            ThemeMode = IsDarkMode ? ThemeMode.Dark : ThemeMode.Light,
            AccentHue = AccentHue,
            AccentChroma = AccentChroma,
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

    [RelayCommand]
    private async Task ApplySaveLogSettingsAsync()
    {
        var (success, message) = await eventLogFacade.UpdateLogSettingsAsync(new LogSettingsSnapshot
        {
            MinLevel = MinLogLevel,
            RetentionDays = RetentionDays,
        });
        notificationRuntime.ApplyShowResult(success, message);
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
        // 誠實回報：更新檢查目前沒有接後端服務可查，不假裝「已是最新版本」，
        // 理由同 RemoteTransferRepository／UsbDeviceVoltageRepository 既有的誠實 stub 慣例。
        notificationRuntime.ApplyShowResult(false, "更新檢查功能尚未串接後端服務");
    }

    #endregion

    #region 工作區管理

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

    /// <summary>刪除工作區會連動砍光該工作區底下所有事件記錄，屬不可逆的破壞性動作——
    /// 比一般刪除確認多一層「輸入完整名稱才能刪除」的摩擦力，跟 usage-patterns.md 一般
    /// 刪除確認（單純 Yes/No）不是同一個判準，這裡刻意做得更嚴格。</summary>
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
