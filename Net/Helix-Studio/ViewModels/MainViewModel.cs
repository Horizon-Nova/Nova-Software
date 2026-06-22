using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Helix_Studio.Data;
using Helix_Studio.Models;
using Helix_Studio.Mvvm;
using Helix_Studio.Services;
using Helix_Studio.Services.Tools;
using Helix_Studio.Views.Dialogs;

namespace Helix_Studio.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ToolProcessService _processService;
    private readonly SystemStatsService _statsService;
    private readonly ProcessSampler _sampler;
    private readonly EventLogService _eventLog;
    private readonly AppSettingsService _appSettings;
    private readonly Dictionary<string, ViewModelBase> _pages;
    private ViewModelBase _currentPage;
    private bool _isToolHostVisible;
    private WorkspaceInfo _workspace;
    private string _searchText = string.Empty;
    private bool _isSearchOpen;
    private bool _isWorkspacePopupOpen;
    private string _currentNavKey = "apps";
    private bool _isSidebarExpanded;
    private bool _isCommandPaletteOpen;
    private string _commandPaletteSearch = string.Empty;
    private int _commandPaletteSelectedIndex = -1;
    private bool _canGoBack;
    private bool _canGoForward;
    private readonly Stack<string> _navBack = new();
    private readonly Stack<string> _navForward = new();
    private CommandPaletteItem[] _allPaletteItems = Array.Empty<CommandPaletteItem>();

    public SystemStats Stats { get; }

    public AppListViewModel AppList { get; }
    public AppLibraryViewModel Library { get; }
    public EventLogViewModel EventLog { get; }
    public SettingsViewModel Settings { get; }
    public ToolHostViewModel ToolHost { get; }

    public string AppVersion { get; }

    public ICommand NavigateCommand { get; }
    public ICommand ToggleWorkspacePopupCommand { get; }
    public ICommand ToggleSidebarCommand { get; }
    public ICommand ActivateWorkspaceCommand { get; }
    public ICommand OpenToolCommand { get; }
    public ICommand ShowToolEventsCommand { get; }
    public ICommand OpenCommandPaletteCommand { get; }
    public ICommand CloseCommandPaletteCommand { get; }
    public ICommand ExecutePaletteItemCommand { get; }
    public ICommand GoBackCommand { get; }
    public ICommand GoForwardCommand { get; }
    public ObservableCollection<CommandPaletteItem> FilteredPaletteItems { get; } = new();

    public ObservableCollection<WorkspaceInfo> Workspaces => Settings.Workspaces;

    public ObservableCollection<InstalledTool> ToolResults { get; } = new();
    public ObservableCollection<EventLogEntry> EventResults { get; } = new();
    public ICommand OpenToolResultCommand { get; }
    public ICommand OpenEventResultCommand { get; }
    public ICommand ShowNotificationsCommand { get; }

    public MainViewModel(ToolCatalogService catalog, ToolInstallService install,
        InstalledToolsRepository repository, EventLogService eventLog, ToolProcessService processService,
        WorkspacesRepository workspacesRepo, AppSettingsService appSettings,
        StartupSettingsService startupSettings, ThemeService themeService)
    {
        _processService = processService;
        _eventLog = eventLog;
        _appSettings = appSettings;
        _isSidebarExpanded = appSettings.Settings.SidebarExpanded;

        AppVersion = "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "—");

        var workspacesFile = workspacesRepo.Load();

        // FL-08 N-002: 依 DefaultWorkspaceId 設定選擇起始工作區
        var startId = appSettings.Settings.DefaultWorkspaceId;
        _workspace = (startId.Length > 0
            ? workspacesFile.Workspaces.FirstOrDefault(w => w.Id == startId)
            : null)
            ?? workspacesFile.Workspaces.FirstOrDefault(w => w.Id == workspacesFile.CurrentId)
            ?? workspacesFile.Workspaces[0];
        _workspace.IsActive = true;

        Stats = new SystemStats();
        _statsService = new SystemStatsService(Stats);
        _statsService.ConfigureAlerts(appSettings, eventLog);
        _statsService.Start();

        eventLog.Initialize(_workspace.Id);

        AppList = new AppListViewModel(catalog, repository, install, appSettings, Stats, _workspace.Id, eventLog);
        Library = new AppLibraryViewModel(catalog, install, repository, _workspace.Id);
        EventLog = new EventLogViewModel(eventLog, appSettings);
        Settings = new SettingsViewModel(workspacesRepo, workspacesFile.Workspaces, _workspace.Id,
            appSettings, startupSettings, themeService);
        ToolHost = new ToolHostViewModel();

        Library.InstalledChanged += AppList.Reload;
        AppList.InstalledChanged += Library.Reload;
        AppList.OpenRequested += OnOpenTool;
        AppList.StopRequested += OnStopTool;
        ToolHost.StopRequested += OnStopTool;
        Settings.SwitchRequested += SwitchWorkspace;
        _processService.ToolExited       += OnToolExited;
        _processService.WindowReady       += OnWindowReady;
        _processService.ToolStatusChanged += OnToolStatusChanged;

        // FL-08 N-001: 語言即時切換後，重整使用轉換器產生文字的視圖（狀態/事件類別/等級/相對時間）
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;

        _sampler = new ProcessSampler(processService);
        _sampler.ConfigureAlerts(appSettings, eventLog);
        _sampler.Sampled += OnToolSampled;
        _sampler.Start();

        _pages = new Dictionary<string, ViewModelBase>
        {
            [AppList.PageKey] = AppList,
            [Library.PageKey] = Library,
            [EventLog.PageKey] = EventLog,
            [Settings.PageKey] = Settings,
        };

        _currentPage = AppList;
        NavigateCommand = new RelayCommand(p => Navigate(p as string));
        ToggleWorkspacePopupCommand = new RelayCommand(_ => IsWorkspacePopupOpen = !IsWorkspacePopupOpen);
        ToggleSidebarCommand = new RelayCommand(_ => IsSidebarExpanded = !IsSidebarExpanded);
        ActivateWorkspaceCommand = new RelayCommand(p =>
        {
            if (p is WorkspaceInfo ws) { SwitchWorkspace(ws); IsWorkspacePopupOpen = false; }
        });
        OpenToolCommand = new RelayCommand(p => { if (p is InstalledTool t) OnOpenTool(t); });
        OpenToolResultCommand = new RelayCommand(p => { if (p is InstalledTool t) OpenToolResult(t); });
        OpenEventResultCommand = new RelayCommand(p => { if (p is EventLogEntry e) OpenEventResult(e); });
        ShowNotificationsCommand = new RelayCommand(_ => Navigate(EventLog.PageKey));
        ShowToolEventsCommand = new RelayCommand(p =>
        {
            if (p is string toolName)
            {
                Navigate(EventLog.PageKey);
                EventLog.SearchText = toolName;
            }
        });
        OpenCommandPaletteCommand = new RelayCommand(_ => OpenCommandPalette());
        CloseCommandPaletteCommand = new RelayCommand(_ => CloseCommandPalette());
        ExecutePaletteItemCommand = new RelayCommand(p => { if (p is CommandPaletteItem i) ExecutePaletteItem(i); });
        GoBackCommand = new RelayCommand(_ => GoBack());
        GoForwardCommand = new RelayCommand(_ => GoForward());

        BuildPaletteItems();
        FilterPaletteItems();

        eventLog.Record(EventLevel.Info, EventCategory.System, "系統", $"Helix Studio 已啟動，工作區「{_workspace.Name}」");

        // FL-08 N-002: 依 ToolStartupBehavior 決定啟動後工具狀態
        ExecuteStartupToolBehavior();
    }

    public WorkspaceInfo Workspace
    {
        get => _workspace;
        private set => SetProperty(ref _workspace, value);
    }

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) RefreshSearch(); }
    }

    public bool IsSearchOpen
    {
        get => _isSearchOpen;
        set => SetProperty(ref _isSearchOpen, value);
    }

    public bool IsWorkspacePopupOpen
    {
        get => _isWorkspacePopupOpen;
        set => SetProperty(ref _isWorkspacePopupOpen, value);
    }

    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        set
        {
            if (SetProperty(ref _isSidebarExpanded, value))
            {
                _appSettings.Settings.SidebarExpanded = value;
                _appSettings.Save();
            }
        }
    }

    public bool IsCommandPaletteOpen
    {
        get => _isCommandPaletteOpen;
        set => SetProperty(ref _isCommandPaletteOpen, value);
    }

    public string CommandPaletteSearch
    {
        get => _commandPaletteSearch;
        set { if (SetProperty(ref _commandPaletteSearch, value)) FilterPaletteItems(); }
    }

    public int CommandPaletteSelectedIndex
    {
        get => _commandPaletteSelectedIndex;
        set => SetProperty(ref _commandPaletteSelectedIndex, value);
    }

    public bool CanGoBack
    {
        get => _canGoBack;
        private set => SetProperty(ref _canGoBack, value);
    }

    public bool CanGoForward
    {
        get => _canGoForward;
        private set => SetProperty(ref _canGoForward, value);
    }

    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public string CurrentNavKey
    {
        get => _currentNavKey;
        private set => SetProperty(ref _currentNavKey, value);
    }

    public string StatusDeviceText => Environment.MachineName;
    public string StatusBuildText => AppVersion;

    public bool IsToolHostVisible
    {
        get => _isToolHostVisible;
        private set => SetProperty(ref _isToolHostVisible, value);
    }

    // FL-08 N-001: 語言切換後刷新各頁轉換器產生的文字，並重建命令面板項目
    private void OnLanguageChanged()
    {
        AppList.RefreshLocalizedText();
        EventLog.RefreshLocalizedText();
        BuildPaletteItems();
        FilterPaletteItems();
    }

    private void Navigate(string? pageKey, bool addToHistory = true)
    {
        if (pageKey is null || !_pages.TryGetValue(pageKey, out var page)) return;

        if (addToHistory && pageKey != _currentNavKey)
        {
            _navBack.Push(_currentNavKey);
            _navForward.Clear();
            UpdateNavButtons();
        }

        CurrentPage = page;
        CurrentNavKey = pageKey;
        IsToolHostVisible = false;
        IsWorkspacePopupOpen = false;
    }

    private void GoBack()
    {
        if (_navBack.Count == 0) return;
        _navForward.Push(_currentNavKey);
        var key = _navBack.Pop();
        Navigate(key, addToHistory: false);
        UpdateNavButtons();
    }

    private void GoForward()
    {
        if (_navForward.Count == 0) return;
        _navBack.Push(_currentNavKey);
        var key = _navForward.Pop();
        Navigate(key, addToHistory: false);
        UpdateNavButtons();
    }

    private void UpdateNavButtons()
    {
        CanGoBack = _navBack.Count > 0;
        CanGoForward = _navForward.Count > 0;
    }

    public void OpenCommandPalette()
    {
        CommandPaletteSearch = string.Empty;
        FilterPaletteItems();
        IsCommandPaletteOpen = true;
    }

    private void CloseCommandPalette()
    {
        IsCommandPaletteOpen = false;
        CommandPaletteSearch = string.Empty;
    }

    public void ExecutePaletteItem(CommandPaletteItem item)
    {
        CloseCommandPalette();
        Navigate(item.NavKey);
        if (item.NavKey == Settings.PageKey && item.Section.Length > 0)
            Application.Current?.Dispatcher.BeginInvoke(
                () => Settings.ActiveSection = item.Section,
                System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void BuildPaletteItems()
    {
        var nav = GetStr("L.Palette.Category.Nav");
        var cfg = GetStr("L.Nav.Settings");
        _allPaletteItems = new[]
        {
            new CommandPaletteItem { Title = GetStr("L.Nav.Apps"),                   Subtitle = nav, IconPath = "/Assets/lucide/layout-grid.svg",  NavKey = "apps" },
            new CommandPaletteItem { Title = GetStr("L.Nav.Library"),                Subtitle = nav, IconPath = "/Assets/lucide/package.svg",       NavKey = "library" },
            new CommandPaletteItem { Title = GetStr("L.Nav.Events"),                 Subtitle = nav, IconPath = "/Assets/lucide/bell.svg",           NavKey = "events" },
            new CommandPaletteItem { Title = GetStr("L.Nav.Settings"),               Subtitle = nav, IconPath = "/Assets/lucide/settings.svg",       NavKey = "settings" },
            new CommandPaletteItem { Title = GetStr("L.Settings.Nav.General"),       Subtitle = cfg, IconPath = "/Assets/lucide/settings.svg",       NavKey = "settings", Section = "General" },
            new CommandPaletteItem { Title = GetStr("L.Settings.Nav.Display"),       Subtitle = cfg, IconPath = "/Assets/lucide/monitor.svg",        NavKey = "settings", Section = "Display" },
            new CommandPaletteItem { Title = GetStr("L.Settings.Nav.Workspaces"),    Subtitle = cfg, IconPath = "/Assets/lucide/layers.svg",         NavKey = "settings", Section = "Workspaces" },
            new CommandPaletteItem { Title = GetStr("L.Settings.Nav.Log"),           Subtitle = cfg, IconPath = "/Assets/lucide/scroll-text.svg",    NavKey = "settings", Section = "Log" },
            new CommandPaletteItem { Title = GetStr("L.Settings.Nav.Alerts"),        Subtitle = cfg, IconPath = "/Assets/lucide/bell.svg",           NavKey = "settings", Section = "Alerts" },
            new CommandPaletteItem { Title = GetStr("L.Settings.Nav.About"),         Subtitle = cfg, IconPath = "/Assets/lucide/info.svg",           NavKey = "settings", Section = "About" },
        };
    }

    private void FilterPaletteItems()
    {
        FilteredPaletteItems.Clear();
        var q = _commandPaletteSearch.Trim();
        var items = q.Length == 0
            ? _allPaletteItems
            : _allPaletteItems.Where(i =>
                i.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Subtitle.Contains(q, StringComparison.OrdinalIgnoreCase));
        foreach (var item in items) FilteredPaletteItems.Add(item);
        CommandPaletteSelectedIndex = FilteredPaletteItems.Count > 0 ? 0 : -1;
    }

    private static string GetStr(string key) => LocalizationService.Instance.GetString(key);

    private void OnOpenTool(InstalledTool tool)
    {
        if (ToolHost.Current == tool && _processService.IsRunning(tool.Id))
        {
            IsToolHostVisible = true;
            return;
        }

        try
        {
            _processService.Start(Workspace.Id, tool);
            tool.LastStarted = DateTime.Now;
            AppList.AppsView.Refresh();

            ToolHost.SetTool(tool, _processService.GetWindowHandle(tool.Id));
            IsToolHostVisible = true;
        }
        catch (Exception)
        {
            tool.Status = AppStatus.Error;
            AppList.AppsView.Refresh();
        }
    }

    private void OnWindowReady(string toolId, nint hwnd)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (ToolHost.Current?.Id == toolId)
                ToolHost.SetTool(ToolHost.Current, hwnd);
        });
    }

    // FL-12 N-004：tool-status.json 輪詢結果 → 更新工具卡片狀態 badge
    private void OnToolStatusChanged(string toolId, AppStatus status, string message)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            var tool = AppList.Apps.FirstOrDefault(t => t.Id == toolId);
            if (tool is null) return;
            tool.Status        = status;
            tool.StatusMessage = message;
        });
    }

    private void OnStopTool(InstalledTool tool)
    {
        IsToolHostVisible = false;
        _processService.Stop(tool.Id);
        if (ToolHost.Current == tool)
            ToolHost.SetTool(tool, nint.Zero);
        tool.Status = AppStatus.Stopped;
        tool.CpuPercent = 0;
        tool.MemoryMb = 0;
        AppList.AppsView.Refresh();
    }

    private void OnToolExited(string toolId)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            var tool = AppList.Apps.FirstOrDefault(t => t.Id == toolId);
            if (tool is null) return;
            if (ToolHost.Current == tool)
            {
                IsToolHostVisible = false;
                ToolHost.SetTool(tool, nint.Zero);
            }
            tool.Status = AppStatus.Stopped;
            tool.CpuPercent = 0;
            tool.MemoryMb = 0;
            AppList.AppsView.Refresh();
        });
    }

    private void OnToolSampled(string toolId, double cpuPercent, double memoryMb)
    {
        var tool = AppList.Apps.FirstOrDefault(t => t.Id == toolId);
        if (tool is not null && tool.IsRunning)
        {
            tool.CpuPercent = cpuPercent;
            tool.MemoryMb = memoryMb;
        }
    }

    private void SwitchWorkspace(WorkspaceInfo ws)
    {
        if (ws.Id == _workspace.Id) return;

        var runningTools = AppList.Apps.Where(a => a.IsRunning).ToList();
        var behavior = _appSettings.Settings.WorkspaceSwitchBehavior;

        if (runningTools.Count > 0 && behavior == "Ask")
        {
            var owner = Application.Current?.MainWindow;
            var names = runningTools.Select(t => t.Name).ToList();
            var result = owner is not null
                ? WorkspaceSwitchDialog.Show(owner, names)
                : SwitchDialogResult.StopAndSwitch;

            switch (result)
            {
                case SwitchDialogResult.Cancel:
                    return;
                case SwitchDialogResult.StopAndSwitch:
                    StopAllAndSwitch(ws);
                    return;
                case SwitchDialogResult.ContinueRunning:
                    DoSwitch(ws);
                    return;
            }
        }
        else if (behavior == "StopNone")
        {
            DoSwitch(ws);
            return;
        }
        else
        {
            StopAllAndSwitch(ws);
        }
    }

    private void StopAllAndSwitch(WorkspaceInfo ws)
    {
        _processService.StopAll();
        IsToolHostVisible = false;
        ToolHost.SetTool(null, nint.Zero);
        DoSwitch(ws);
    }

    private void DoSwitch(WorkspaceInfo ws)
    {
        foreach (var w in Settings.Workspaces) w.IsActive = w.Id == ws.Id;

        Workspace = ws;
        _eventLog.Initialize(ws.Id);
        AppList.SwitchWorkspace(ws.Id);
        Library.SwitchWorkspace(ws.Id);

        Settings.CurrentWorkspaceId = ws.Id;
        Settings.Save();

        CurrentPage = AppList;
        _eventLog.Record(EventLevel.Info, EventCategory.System, "系統", $"已切換至工作區「{ws.Name}」");

        // FL-03 N-006: 自動啟動新工作區的工具
        _ = ExecuteAutoStartAsync(ws.Id);
    }

    // FL-03 N-006: 依序自動啟動 AutoStart 工具（500ms 間隔）
    private async Task ExecuteAutoStartAsync(string workspaceId)
    {
        await Task.Delay(200);
        foreach (var tool in AppList.GetAutoStartTools())
        {
            if (tool.IsRunning) continue;
            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    _processService.Start(workspaceId, tool);
                    tool.LastStarted = DateTime.Now;
                    AppList.AppsView.Refresh();
                    _eventLog.Record(EventLevel.Info, EventCategory.Runtime, tool.Name, "已自動啟動");
                }
                catch
                {
                    tool.Status = AppStatus.Error;
                    _eventLog.Record(EventLevel.Error, EventCategory.Runtime, tool.Name, "自動啟動失敗");
                }
            });
            await Task.Delay(500);
        }
    }

    // FL-08 N-002: 應用程式啟動時的工具行為
    private void ExecuteStartupToolBehavior()
    {
        var behavior = _appSettings.Settings.ToolStartupBehavior;
        if (behavior == "autostart")
        {
            _ = ExecuteAutoStartAsync(_workspace.Id);
        }
        else if (behavior == "restore")
        {
            var lastRunning = _appSettings.Settings.LastRunningToolIds;
            if (lastRunning.TryGetValue(_workspace.Id, out var ids) && ids.Count > 0)
                _ = RestoreLastRunningAsync(_workspace.Id, ids);
        }
    }

    private async Task RestoreLastRunningAsync(string workspaceId, IReadOnlyList<string> toolIds)
    {
        await Task.Delay(200);
        foreach (var id in toolIds)
        {
            var tool = AppList.Apps.FirstOrDefault(t => t.Id == id);
            if (tool is null || tool.IsRunning) continue;
            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    _processService.Start(workspaceId, tool);
                    tool.LastStarted = DateTime.Now;
                    AppList.AppsView.Refresh();
                }
                catch
                {
                    tool.Status = AppStatus.Error;
                }
            });
            await Task.Delay(500);
        }
    }

    // FL-08 N-002: 正常關閉時儲存目前執行中工具清單
    public void SaveLastRunningTools()
    {
        var ids = AppList.Apps
            .Where(a => a.Status == AppStatus.Running)
            .Select(a => a.Id)
            .ToList();
        _appSettings.Settings.LastRunningToolIds[_workspace.Id] = ids;
        _appSettings.Save();
    }

    private void RefreshSearch()
    {
        ToolResults.Clear();
        EventResults.Clear();

        var q = _searchText.Trim();
        if (q.Length > 0)
        {
            foreach (var tool in AppList.Apps
                         .Where(a => a.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(6))
                ToolResults.Add(tool);

            foreach (var entry in _eventLog.Entries
                         .Where(e => e.Message.Contains(q, StringComparison.OrdinalIgnoreCase)
                                     || e.Source.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(6))
                EventResults.Add(entry);
        }

        IsSearchOpen = q.Length > 0 && (ToolResults.Count > 0 || EventResults.Count > 0);
    }

    private void OpenToolResult(InstalledTool tool)
    {
        Navigate(AppList.PageKey);
        AppList.DetailTool = tool;
        SearchText = string.Empty;
    }

    private void OpenEventResult(EventLogEntry entry)
    {
        var term = _searchText.Trim();
        Navigate(EventLog.PageKey);
        EventLog.SearchText = term;
        SearchText = string.Empty;
    }
}
