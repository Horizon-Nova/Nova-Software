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

/// <summary>殼層協調者：導覽切換、工作區狀態、資源用量</summary>
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
    NavigationRuntime navigationRuntime,
    PowerMenuRuntime powerMenuRuntime) : ObservableObject
{
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();
    private Dictionary<string, object> _pages = [];
    private string? _currentKey;

    /// <summary>骨架屏最短可視時間</summary>
    private static readonly TimeSpan MinVisibleLoadingDuration = TimeSpan.FromMilliseconds(400);

    /// <summary>換頁過渡骨架屏可視時間</summary>
    private static readonly TimeSpan NavTransitionDuration = TimeSpan.FromMilliseconds(420);

    /// <summary>本機資源用量輪詢計時器</summary>
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
    /// <summary>側邊欄展開寬度（180–280px）</summary>
    [ObservableProperty] private double _sidebarWidth = 224;
    [ObservableProperty] private WorkspaceSnapshot? _activeWorkspace;
    [ObservableProperty] private IReadOnlyList<WorkspaceSnapshot> _workspaces = [];
    [ObservableProperty] private int _runningToolCount;
    [ObservableProperty] private int _unreadEventCount;
    /// <summary>工作區事件總數（底部狀態列）</summary>
    [ObservableProperty] private int _totalEventCount;
    [ObservableProperty] private bool _canGoBack;
    [ObservableProperty] private bool _canGoForward;
    [ObservableProperty] private string? _currentNavKey;
    [ObservableProperty] private double _ramUsedMb;
    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private bool _showServiceBanner;
    [ObservableProperty] private string _serviceBannerText = "";
    [ObservableProperty] private bool _switcherOpen;
    /// <summary>切換工作區骨架屏</summary>
    [ObservableProperty] private bool _isSwitchingWorkspace;
    /// <summary>換頁過渡骨架屏</summary>
    [ObservableProperty] private bool _isNavigating;
    /// <summary>介面縮放比例（1.0 = 100%）</summary>
    [ObservableProperty] private double _uiScale = 1.0;
    /// <summary>未讀事件徽章顯示開關</summary>
    [ObservableProperty] private bool _showEventBadge = true;
    /// <summary>使用者偏好：停用背景動畫（優先權高於主題包宣告）</summary>
    [ObservableProperty] private bool _backgroundAnimationEnabled = true;
    /// <summary>闔螢幕動作偏好，供 MainWindow 攔截 GUID_LIDSWITCH_STATE_CHANGE 時判斷要不要跳電源選單——
    /// 只有這個值等於「顯示關閉選單」時才跳，其餘值（含「不執行動作」）Windows 自己不做事，App 也不做事。</summary>
    [ObservableProperty] private string _lidCloseAction = "睡眠";

    /// <summary>視窗最小尺寸（100% 縮放下的底線）</summary>
    public double EffectiveMinWidth => 900;
    public double EffectiveMinHeight => 600;

    /// <summary>非目前作用中的工作區清單</summary>
    public IEnumerable<WorkspaceSnapshot> OtherWorkspaces
        => Workspaces.Where(w => ActiveWorkspace is null || w.Id != ActiveWorkspace.Id);

    /// <summary>側邊欄實際寬度（摺疊 56px／展開 SidebarWidth）</summary>
    public double EffectiveSidebarWidth => SidebarCollapsed ? 56 : SidebarWidth;

    /// <summary>未讀徽章是否顯示</summary>
    public bool HasVisibleUnreadBadge => ShowEventBadge && UnreadEventCount > 0;

    partial void OnSidebarCollapsedChanged(bool value) => OnPropertyChanged(nameof(EffectiveSidebarWidth));
    partial void OnSidebarWidthChanged(double value) => OnPropertyChanged(nameof(EffectiveSidebarWidth));
    partial void OnShowEventBadgeChanged(bool value) => OnPropertyChanged(nameof(HasVisibleUnreadBadge));
    partial void OnUnreadEventCountChanged(int value) => OnPropertyChanged(nameof(HasVisibleUnreadBadge));

    /// <summary>拖曳調整側邊欄寬度</summary>
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

        ApplyNavigateImmediate("apps");
        _ = ApplyRefreshStatusBarAsync();
        _ = ApplySettingsSnapshotAsync();
    }

    /// <summary>解除 ApplyShellLoaded 的訂閱</summary>
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

    /// <summary>套用介面縮放／未讀徽章／側邊欄摺疊設定</summary>
    private async Task ApplySettingsSnapshotAsync()
    {
        var settings = await settingsFacade.LoadSettingsAsync();
        UiScale = settings.UiScalePercent / 100.0;
        ShowEventBadge = settings.ShowEventBadge;
        SidebarCollapsed = settings.SidebarCollapsedByDefault;
        BackgroundAnimationEnabled = settings.BackgroundAnimationEnabled;
        LidCloseAction = settings.LidCloseAction;
    }

    /// <summary>MainWindow 攔截到闔螢幕（GUID_LIDSWITCH_STATE_CHANGE）且 LidCloseAction＝「顯示關閉選單」時呼叫；
    /// 跳出電源選單（PowerMenuRuntime），使用者選定後立即執行對應系統動作。不寫裸 async void 例外處理——
    /// 這裡跟 Feature ViewModel 的 RunSafe 白名單機制不是同一套（MainShellViewModel 不繼承 ViewModelBase，
    /// 比照本檔其餘事件回呼既有寫法，如 OnWorkspaceChanged）。</summary>
    public async void ApplyLidClosedShowMenu()
    {
        var action = await powerMenuRuntime.ShowAsync();
        switch (action)
        {
            case "睡眠": await settingsFacade.ApplyExecuteSleepNowAsync(); break;
            case "休眠": await settingsFacade.ApplyExecuteHibernateNowAsync(); break;
            case "關機": await settingsFacade.ApplyExecuteShutdownNowAsync(); break;
        }
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

    /// <summary>依目前畫面與服務狀態計算頂部服務橫幅</summary>
    private void ApplyServiceBanner()
    {
        var usbTool = _lastTools.FirstOrDefault(t => t.Name == "USB 防休眠控制");
        var vncTool = _lastTools.FirstOrDefault(t => t.Name == "遠端批次傳檔");

        (ShowServiceBanner, ServiceBannerText) = CurrentNavKey switch
        {
            "usb" when usbTool is { IsRunning: false } => (true, "服務目前已停止，下方電壓修正不會實際生效，請先啟動服務。"),
            "vnc" when vncTool is { IsRunning: false } => (true, "服務目前已停止，無法發布任務，請先啟動服務。"),
            _ => (false, ""),
        };
    }

    /// <summary>啟動導覽，不經過換頁過渡骨架屏</summary>
    private void ApplyNavigateImmediate(string key)
    {
        if (_currentKey == key) return;
        if (_currentKey is not null) _backStack.Push(_currentKey);
        _forwardStack.Clear();
        SetCurrent(key);
    }

    /// <summary>換頁過渡</summary>
    [RelayCommand]
    private async Task ApplyNavigateAsync(string key)
    {
        if (_currentKey == key) return;
        IsNavigating = true;
        await Task.Delay(NavTransitionDuration);
        ApplyNavigateImmediate(key);
        IsNavigating = false;
    }

    [RelayCommand]
    private async Task ApplyGoBackAsync()
    {
        if (_backStack.Count == 0) return;
        IsNavigating = true;
        await Task.Delay(NavTransitionDuration);
        if (_currentKey is not null) _forwardStack.Push(_currentKey);
        SetCurrent(_backStack.Pop());
        IsNavigating = false;
    }

    [RelayCommand]
    private async Task ApplyGoForwardAsync()
    {
        if (_forwardStack.Count == 0) return;
        IsNavigating = true;
        await Task.Delay(NavTransitionDuration);
        if (_currentKey is not null) _backStack.Push(_currentKey);
        SetCurrent(_forwardStack.Pop());
        IsNavigating = false;
    }

    [RelayCommand]
    private void ApplyToggleSidebar() => ApplySetSidebarCollapsed(!SidebarCollapsed);

    /// <summary>側邊欄摺疊狀態的唯一寫入點</summary>
    private void ApplySetSidebarCollapsed(bool collapsed)
    {
        SidebarCollapsed = collapsed;
        _ = settingsFacade.UpdateSidebarCollapsedDefaultAsync(collapsed);
    }

    [RelayCommand]
    private async Task ApplyFocusSearchAsync()
    {
        await ApplyNavigateAsync("apps");
        appsView.FocusSearchBox();
    }

    [RelayCommand]
    private async Task ApplySwitchWorkspaceAsync(WorkspaceSnapshot workspace)
    {
        SwitcherOpen = false;
        IsSwitchingWorkspace = true;

        var stopwatch = Stopwatch.StartNew();
        await workspaceFacade.SwitchActiveWorkspaceAsync(workspace.Id);
        var remaining = MinVisibleLoadingDuration - stopwatch.Elapsed;
        if (remaining > TimeSpan.Zero)
            await Task.Delay(remaining);

        IsSwitchingWorkspace = false;
    }

    /// <summary>切換工作區選單顯示（摺疊時先展開側邊欄）</summary>
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
