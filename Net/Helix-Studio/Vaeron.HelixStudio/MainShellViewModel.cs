using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vaeron.HelixStudio.Features.AppLibrary;
using Vaeron.HelixStudio.Features.AppsManagement;
using Vaeron.HelixStudio.Features.EventLog;
using Vaeron.HelixStudio.Features.RemoteBatchTransfer;
using Vaeron.HelixStudio.Features.Settings;
using Vaeron.HelixStudio.Features.UsbVoltageControl;
using Vaeron.HelixStudio.Features.WorkspaceManagement;
using Vaeron.HelixStudio.Features.WorkspaceManagement.Contracts;
using Vaeron.Platform.AppRuntime;

namespace Vaeron.HelixStudio;

public sealed record NavItem(string Key, string Label, bool HasIcon);

/// <summary>App 根層級的殼層協調者——不是 Feature，持有全部畫面 View 的參考（已由 DI 注入完成
/// 各自的 ViewModel 建構），依導覽 key 切換 CurrentView。用直接持有 View 實例、不透過
/// DataTemplate 隱式建立——DataTemplate 隱式建立需要目標型別有無參建構子，跟這裡每個 Feature
/// View 的建構子強制注入 ViewModel（reference-implementation.md 既定模式）互斥，故不採用。</summary>
public sealed partial class MainShellViewModel(
    AppsManagementView appsView,
    AppLibraryView libraryView,
    EventLogView eventLogView,
    SettingsView settingsView,
    UsbVoltageControlView usbView,
    RemoteBatchTransferView remoteBatchTransferView,
    WorkspaceManagementFacade workspaceFacade,
    AppsManagementFacade appsManagement,
    EventLogFacade eventLogFacade,
    SettingsFacade settingsFacade,
    NavigationRuntime navigationRuntime) : ObservableObject
{
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();
    private Dictionary<string, object> _pages = [];
    private string? _currentKey;

    /// <summary>本機資源用量（RAM/CPU）是殼層自己讀取本進程數值，不是工作區資料的一部分——
    /// 不塞進 WorkspaceSnapshot（contracts.md：Snapshot 只承載該 Feature 自己的資料形狀），
    /// 用獨立 Timer 輪詢，跟工作區切換/事件變更的節奏無關。</summary>
    private readonly DispatcherTimer _resourceStatsTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Process _currentProcess = Process.GetCurrentProcess();
    private TimeSpan _lastCpuTime = TimeSpan.Zero;
    private DateTimeOffset _lastCpuSampleAt = DateTimeOffset.UtcNow;
    private IReadOnlyList<Features.AppsManagement.Contracts.InstalledToolSnapshot> _lastTools = [];

    public ObservableCollection<NavItem> MainNav { get; } =
    [
        new("apps", "已安裝工具", true),
        new("library", "工具庫", true),
        new("events", "事件日誌", true),
        new("settings", "設定", true),
    ];

    public ObservableCollection<NavItem> ToolsNav { get; } =
    [
        new("usb", "USB 防休眠控制", false),
        new("vnc", "遠端批次傳檔", false),
    ];

    [ObservableProperty] private object? _currentView;
    [ObservableProperty] private bool _sidebarCollapsed;
    /// <summary>展開時側邊欄的實際寬度，可拖曳調整（比照 blueprint sidebarWidth state，
    /// 180–280px 範圍），跟摺疊時固定 56px 是兩件事——EffectiveSidebarWidth 才是 View 實際
    /// 綁定的寬度，這個欄位只在展開狀態下才有意義。</summary>
    [ObservableProperty] private double _sidebarWidth = 224;
    [ObservableProperty] private WorkspaceSnapshot? _activeWorkspace;
    [ObservableProperty] private IReadOnlyList<WorkspaceSnapshot> _workspaces = [];
    [ObservableProperty] private int _runningToolCount;
    [ObservableProperty] private int _unreadEventCount;
    /// <summary>底部狀態列「事件 N」顯示的是這個工作區累計事件總數（blueprint 的 ws.eventCount），
    /// 跟通知鈴鐺上的未讀紅點數（UnreadEventCount，對應 ws.eventBadge）是兩個不同的數字，
    /// 不能共用同一個屬性——已讀完事件後未讀數會歸零，但底部的累計總數不會跟著變。</summary>
    [ObservableProperty] private int _totalEventCount;
    [ObservableProperty] private bool _canGoBack;
    [ObservableProperty] private bool _canGoForward;
    [ObservableProperty] private string? _currentNavKey;
    [ObservableProperty] private double _ramUsedMb;
    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private bool _showServiceBanner;
    [ObservableProperty] private string _serviceBannerText = "";
    [ObservableProperty] private bool _switcherOpen;
    /// <summary>設定畫面「介面縮放」的即時套用結果——MainWindow 的 LayoutTransform 綁這個值。
    /// 1.0 = 100%，來源是 SettingsFacade（唯一入口，見 SettingsFacade 的 doc comment）。</summary>
    [ObservableProperty] private double _uiScale = 1.0;
    /// <summary>設定畫面「顯示未讀事件徽章」的即時套用結果——鈴鐺紅點的 Visibility 除了看
    /// UnreadEventCount，還要看這個開關有沒有被關掉。</summary>
    [ObservableProperty] private bool _showEventBadge = true;

    /// <summary>對齊 blueprint 的 otherWorkspaces：切換清單只列出「不是目前作用中」的工作區，
    /// 目前作用中的已經顯示在按鈕本體上，不需要在清單裡重複出現一次。</summary>
    public IEnumerable<WorkspaceSnapshot> OtherWorkspaces
        => Workspaces.Where(w => ActiveWorkspace is null || w.Id != ActiveWorkspace.Id);

    /// <summary>View 實際綁定的側邊欄寬度——摺疊時固定 56px（圖示列），展開時用可拖曳調整的
    /// SidebarWidth。兩個來源合一成一個屬性，View 端不需要再自己判斷摺疊狀態決定要看哪個值。</summary>
    public double EffectiveSidebarWidth => SidebarCollapsed ? 56 : SidebarWidth;

    /// <summary>頂部列鈴鐺紅點實際要不要顯示——除了「有未讀」，還要「使用者沒有把這個徽章關掉」，
    /// 兩個條件合一成一個屬性，View 端只需要綁這一個，不必自己再組 MultiBinding。</summary>
    public bool HasVisibleUnreadBadge => ShowEventBadge && UnreadEventCount > 0;

    partial void OnSidebarCollapsedChanged(bool value) => OnPropertyChanged(nameof(EffectiveSidebarWidth));
    partial void OnSidebarWidthChanged(double value) => OnPropertyChanged(nameof(EffectiveSidebarWidth));
    partial void OnShowEventBadgeChanged(bool value) => OnPropertyChanged(nameof(HasVisibleUnreadBadge));
    partial void OnUnreadEventCountChanged(int value) => OnPropertyChanged(nameof(HasVisibleUnreadBadge));

    /// <summary>拖曳側邊欄右緣調整寬度，對齊 blueprint startResize/onDragMove 的
    /// Math.max(180, Math.min(280, ...)) 夾限，避免拖到太窄擠壞導覽項目、或太寬吃掉內容區。</summary>
    public void ApplyResizeSidebar(double deltaX)
        => SidebarWidth = Math.Clamp(SidebarWidth + deltaX, 180, 280);

    public void ApplyShellLoaded()
    {
        _pages = new Dictionary<string, object>
        {
            ["apps"] = appsView,
            ["library"] = libraryView,
            ["events"] = eventLogView,
            ["settings"] = settingsView,
            ["usb"] = usbView,
            ["vnc"] = remoteBatchTransferView,
        };

        workspaceFacade.WorkspaceChanged += OnWorkspaceChanged;
        appsManagement.ToolsChanged += OnStatusChanged;
        eventLogFacade.EventsChanged += OnStatusChanged;
        settingsFacade.SettingsChanged += OnSettingsChanged;

        _resourceStatsTimer.Tick += OnResourceStatsTick;
        _resourceStatsTimer.Start();
        OnResourceStatsTick(this, EventArgs.Empty);

        ApplyNavigate("apps");
        _ = ApplyRefreshStatusBarAsync();
        _ = ApplySettingsSnapshotAsync();
    }

    /// <summary>與 ApplyShellLoaded() 成對——即使 Shell/Facade 皆為 Singleton、實務上洩漏風險低，
    /// 訂閱與解除訂閱仍要當一對稽核（viewmodels.md 必須條款），不因為這裡剛好風險低就跳過。</summary>
    public void ApplyShellUnloaded()
    {
        workspaceFacade.WorkspaceChanged -= OnWorkspaceChanged;
        appsManagement.ToolsChanged -= OnStatusChanged;
        eventLogFacade.EventsChanged -= OnStatusChanged;
        settingsFacade.SettingsChanged -= OnSettingsChanged;
        _resourceStatsTimer.Tick -= OnResourceStatsTick;
        _resourceStatsTimer.Stop();
    }

    private async void OnWorkspaceChanged() => await ApplyRefreshStatusBarAsync();
    private async void OnStatusChanged() => await ApplyRefreshStatusBarAsync();
    private async void OnSettingsChanged() => await ApplySettingsSnapshotAsync();

    /// <summary>SettingsFacade 是殼層讀取「介面縮放」「顯示未讀事件徽章」「側邊欄摺疊」
    /// 這幾個跨畫面設定的唯一入口（見 SettingsFacade 的 doc comment）。側邊欄摺疊在 blueprint
    /// 裡不是獨立的「下次啟動預設值」，是跟這裡的 SidebarCollapsed 同一顆狀態（Settings 畫面
    /// 的開關呼叫的是同一個 onToggleSidebar）——所以這裡讀到的值直接套用，不是只在啟動時讀一次；
    /// 真正維持兩邊同步的另一半在 ApplySetSidebarCollapsed，每次即時切換都會寫回資料庫。</summary>
    private async Task ApplySettingsSnapshotAsync()
    {
        var settings = await settingsFacade.LoadSettingsAsync();
        UiScale = settings.UiScalePercent / 100.0;
        ShowEventBadge = settings.ShowEventBadge;
        SidebarCollapsed = settings.SidebarCollapsedByDefault;
    }

    private void OnResourceStatsTick(object? sender, EventArgs e)
    {
        _currentProcess.Refresh();
        RamUsedMb = _currentProcess.WorkingSet64 / 1024.0 / 1024.0;

        var now = DateTimeOffset.UtcNow;
        var cpuTime = _currentProcess.TotalProcessorTime;
        var wallElapsed = now - _lastCpuSampleAt;
        if (wallElapsed.TotalMilliseconds > 0 && _lastCpuTime != TimeSpan.Zero)
        {
            var cpuElapsed = cpuTime - _lastCpuTime;
            CpuPercent = Math.Clamp(cpuElapsed.TotalMilliseconds / (wallElapsed.TotalMilliseconds * Environment.ProcessorCount) * 100, 0, 100);
        }
        _lastCpuTime = cpuTime;
        _lastCpuSampleAt = now;
    }

    partial void OnWorkspacesChanged(IReadOnlyList<WorkspaceSnapshot> value) => OnPropertyChanged(nameof(OtherWorkspaces));
    partial void OnActiveWorkspaceChanged(WorkspaceSnapshot? value) => OnPropertyChanged(nameof(OtherWorkspaces));

    private async Task ApplyRefreshStatusBarAsync()
    {
        Workspaces = await workspaceFacade.LoadWorkspaceListAsync();
        ActiveWorkspace = await workspaceFacade.LoadActiveWorkspaceAsync();

        if (ActiveWorkspace is null) return;

        _lastTools = await appsManagement.LoadInstalledToolListAsync(ActiveWorkspace.Id);
        RunningToolCount = _lastTools.Count(t => t.IsRunning);
        UnreadEventCount = await eventLogFacade.LoadUnreadCountAsync(ActiveWorkspace.Id);
        TotalEventCount = (await eventLogFacade.LoadEventListAsync(ActiveWorkspace.Id)).Count;
        ApplyServiceBanner();
    }

    /// <summary>對齊 blueprint Layout.dc.html 的 serviceBanner：依「目前作用中畫面」＋「該畫面
    /// 對應服務是否執行中」計算，畫在殼層頂部（頂部列上方），不是各子畫面各自畫一個——
    /// 之前 UsbVoltageControl／RemoteBatchTransfer 子畫面各自內嵌 ProcessGatedBanner 是誤植，
    /// 已移除，橫幅只在這一個地方決定要不要顯示。</summary>
    private void ApplyServiceBanner()
    {
        // 比照 blueprint `usbApp &amp;&amp; usbApp.state !== 'running'`：工具根本沒安裝在這個工作區時
        // (tool 為 null) 不顯示橫幅，只有「有裝但目前沒在跑」才顯示——沒裝跟停用是兩件不同的事。
        var usbTool = _lastTools.FirstOrDefault(t => t.Name == "USB 防休眠控制");
        var vncTool = _lastTools.FirstOrDefault(t => t.Name == "遠端批次傳檔");

        (ShowServiceBanner, ServiceBannerText) = CurrentNavKey switch
        {
            "usb" when usbTool is { IsRunning: false } => (true, "服務目前已停止，下方電壓修正不會實際生效，請先啟動服務。"),
            "vnc" when vncTool is { IsRunning: false } => (true, "服務目前已停止，無法發布任務，請先啟動服務。"),
            _ => (false, ""),
        };
    }

    [RelayCommand]
    private void ApplyNavigate(string key)
    {
        if (_currentKey == key) return;

        if (_currentKey is not null) _backStack.Push(_currentKey);
        _forwardStack.Clear();
        SetCurrent(key);
    }

    [RelayCommand]
    private void ApplyGoBack()
    {
        if (_backStack.Count == 0) return;
        if (_currentKey is not null) _forwardStack.Push(_currentKey);
        SetCurrent(_backStack.Pop());
    }

    [RelayCommand]
    private void ApplyGoForward()
    {
        if (_forwardStack.Count == 0) return;
        if (_currentKey is not null) _backStack.Push(_currentKey);
        SetCurrent(_forwardStack.Pop());
    }

    [RelayCommand]
    private void ApplyToggleSidebar() => ApplySetSidebarCollapsed(!SidebarCollapsed);

    /// <summary>側邊欄摺疊/展開的唯一寫入點——不管是頂部漢堡選單、Ctrl+B、還是摺疊時點工作區
    /// 切換器，全部走這裡，不是各自直接改 SidebarCollapsed。blueprint 的「預設摺疊側邊欄」
    /// 開關（Settings 畫面）跟這裡是同一顆狀態，所以每次即時切換都要順便寫回資料庫，
    /// 不是只在應用程式啟動時讀一次——兩邊要一直保持同步。</summary>
    private void ApplySetSidebarCollapsed(bool collapsed)
    {
        SidebarCollapsed = collapsed;
        _ = settingsFacade.UpdateSidebarCollapsedDefaultAsync(collapsed);
    }

    [RelayCommand]
    private void ApplyFocusSearch()
    {
        ApplyNavigate("apps");
        appsView.FocusSearchBox();
    }

    [RelayCommand]
    private async Task ApplySwitchWorkspaceAsync(WorkspaceSnapshot workspace)
    {
        SwitcherOpen = false;
        await workspaceFacade.SwitchActiveWorkspaceAsync(workspace.Id);
    }

    /// <summary>側邊欄摺疊時按這顆鈕不是開切換清單彈窗（56px 圖示列擠不下清單卡片），
    /// 是先把側邊欄展開，讓使用者看得到完整介面再自己決定要不要切換工作區。</summary>
    [RelayCommand]
    private void ApplyToggleSwitcher()
    {
        if (SidebarCollapsed)
        {
            ApplySetSidebarCollapsed(false);
            return;
        }
        SwitcherOpen = !SwitcherOpen;
    }

    [RelayCommand]
    private void ApplyCloseSwitcher() => SwitcherOpen = false;

    private void SetCurrent(string key)
    {
        _currentKey = key;
        CurrentNavKey = key;
        CurrentView = _pages.TryGetValue(key, out var view) ? view : null;
        CanGoBack = _backStack.Count > 0;
        CanGoForward = _forwardStack.Count > 0;
        navigationRuntime.ApplyNavigateToModule(key);
        ApplyServiceBanner();

        if (key == "events" && ActiveWorkspace is not null)
            _ = eventLogFacade.MarkAsReadAsync(ActiveWorkspace.Id);
    }
}
