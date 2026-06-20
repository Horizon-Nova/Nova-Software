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

        // FL-16: 依 DefaultWorkspaceId 設定選擇起始工作區
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
        _processService.ToolExited += OnToolExited;
        _processService.WindowReady += OnWindowReady;

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

        eventLog.Record(EventLevel.Info, EventCategory.System, "系統", $"Helix Studio 已啟動，工作區「{_workspace.Name}」");

        // FL-16: 依 ToolStartupBehavior 決定啟動後工具狀態
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

    private void Navigate(string? pageKey)
    {
        if (pageKey is not null && _pages.TryGetValue(pageKey, out var page))
        {
            CurrentPage = page;
            CurrentNavKey = pageKey;
            IsToolHostVisible = false;
            IsWorkspacePopupOpen = false;
        }
    }

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
            tool.Status = AppStatus.Running;
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
        if (tool is not null && tool.Status == AppStatus.Running)
        {
            tool.CpuPercent = cpuPercent;
            tool.MemoryMb = memoryMb;
        }
    }

    private void SwitchWorkspace(WorkspaceInfo ws)
    {
        if (ws.Id == _workspace.Id) return;

        var runningTools = AppList.Apps.Where(a => a.Status == AppStatus.Running).ToList();
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
        else if (behavior == "ContinueRunning")
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

        // FL-14: 自動啟動新工作區的工具
        _ = ExecuteAutoStartAsync(ws.Id);
    }

    // FL-14: 依序自動啟動 AutoStart 工具（500ms 間隔）
    private async Task ExecuteAutoStartAsync(string workspaceId)
    {
        await Task.Delay(200);
        foreach (var tool in AppList.GetAutoStartTools())
        {
            if (tool.Status == AppStatus.Running) continue;
            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    _processService.Start(workspaceId, tool);
                    tool.Status = AppStatus.Running;
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

    // FL-16: 應用程式啟動時的工具行為
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
            if (tool is null || tool.Status == AppStatus.Running) continue;
            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    _processService.Start(workspaceId, tool);
                    tool.Status = AppStatus.Running;
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

    // FL-16: 正常關閉時儲存目前執行中工具清單
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
