using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Helix_Studio.Model.Notifications;
using Helix_Studio.Services;
using Helix_Studio.ViewModels.Base;
using HorizonNova.HelixStudio.Contracts;

namespace Helix_Studio.ViewModels;

/// <summary>
/// MainWindow 的 UI 狀態機，負責工作區切換、模組導覽與狀態列顯示。
/// </summary>
public sealed class MainWindowViewModel(MainWindowServices services) : ViewModelBase, IDisposable
{
    private const int WorkspaceSwitchShimmerMinimumMilliseconds = 200;
    private SynchronizationContext _synchronizationContext = new();
    private RelayCommand? _activateWorkspaceCommand;
    private RelayCommand? _toggleWorkspacePopupCommand;
    private RelayCommand? _clearInboxCommand;
    private bool _isInitialized;
    private bool _isDisposed;
    private string _activeWorkspaceName = string.Empty;
    private bool _isWorkspacePopupOpen;
    private bool _isWorkspaceSwitching;
    private int _workspaceSwitchToken;
    private long _workspaceSwitchStartedTimestamp;
    private bool _isModulesLoading;
    private string _workspaceBreadcrumbText = "—";
    private string _breadcrumbText = "Home";
    private string _statusWorkspaceText = "工作區：未選取";
    private string _statusModulesText = "模組：0";
    private string _statusDeviceText = $"裝置：{Environment.MachineName}";
    private string _statusBuildText = $"版本：{Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "未知"}";
    private string _currentModuleHealth = "Healthy";
    private string _currentModuleHealthMessage = "模組狀態正常";
    private string? _contentModuleId;

    /// <summary>
    /// 提供給 View 顯示的工作區清單。
    /// </summary>
    public ObservableCollection<WorkspaceItem> Workspaces { get; } = new();

    /// <summary>
    /// 提供給 View 顯示的模組導覽清單。
    /// </summary>
    public ObservableCollection<ModuleNavItem> ModuleNavItems { get; } = new();

    /// <summary>
    /// 通知收件匣清單。
    /// </summary>
    public ObservableCollection<ShellInboxItem> InboxItems { get; } = new();

    /// <summary>
    /// 上方導覽列的工作區麵包屑文字。
    /// </summary>
    public string WorkspaceBreadcrumbText
    {
        get => _workspaceBreadcrumbText;
        private set => SetField(ref _workspaceBreadcrumbText, value);
    }

    /// <summary>
    /// 上方導覽列的頁面/模組麵包屑文字。
    /// </summary>
    public string BreadcrumbText
    {
        get => _breadcrumbText;
        private set => SetField(ref _breadcrumbText, value);
    }

    /// <summary>
    /// 目前啟用的工作區名稱。
    /// </summary>
    public string ActiveWorkspaceName
    {
        get => _activeWorkspaceName;
        private set => SetField(ref _activeWorkspaceName, value);
    }

    /// <summary>
    /// 是否已有模組導覽項目。
    /// </summary>
    public bool HasModuleNavItems => ModuleNavItems.Count > 0;

    /// <summary>
    /// 是否開啟工作區選單。
    /// </summary>
    public bool IsWorkspacePopupOpen
    {
        get => _isWorkspacePopupOpen;
        set => SetField(ref _isWorkspacePopupOpen, value);
    }

    /// <summary>
    /// 是否顯示工作區切換中的提示。
    /// </summary>
    public bool IsWorkspaceSwitching
    {
        get => _isWorkspaceSwitching;
        private set => SetField(ref _isWorkspaceSwitching, value);
    }

    /// <summary>
    /// 是否顯示模組載入中的提示。
    /// </summary>
    public bool IsModulesLoading
    {
        get => _isModulesLoading;
        private set => SetField(ref _isModulesLoading, value);
    }

    /// <summary>
    /// 狀態列的工作區文字。
    /// </summary>
    public string StatusWorkspaceText
    {
        get => _statusWorkspaceText;
        private set => SetField(ref _statusWorkspaceText, value);
    }

    /// <summary>
    /// 狀態列的模組數量文字。
    /// </summary>
    public string StatusModulesText
    {
        get => _statusModulesText;
        private set => SetField(ref _statusModulesText, value);
    }

    /// <summary>
    /// 狀態列的裝置文字。
    /// </summary>
    public string StatusDeviceText
    {
        get => _statusDeviceText;
        private set => SetField(ref _statusDeviceText, value);
    }

    /// <summary>
    /// 狀態列的版本文字。
    /// </summary>
    public string StatusBuildText
    {
        get => _statusBuildText;
        private set => SetField(ref _statusBuildText, value);
    }

    /// <summary>
    /// 目前模組健康狀態。
    /// </summary>
    public string CurrentModuleHealth
    {
        get => _currentModuleHealth;
        private set => SetField(ref _currentModuleHealth, value);
    }

    /// <summary>
    /// 目前模組健康狀態訊息。
    /// </summary>
    public string CurrentModuleHealthMessage
    {
        get => _currentModuleHealthMessage;
        private set => SetField(ref _currentModuleHealthMessage, value);
    }

    /// <summary>
    /// 是否顯示通知徽章。
    /// </summary>
    public bool ShowNotificationBadge => UnreadCount > 0;

    /// <summary>
    /// 未讀通知數量。
    /// </summary>
    public int UnreadCount => InboxItems.Count;

    /// <summary>
    /// 是否有通知項目。
    /// </summary>
    public bool HasInboxItems => InboxItems.Count > 0;

    /// <summary>
    /// 是否顯示空收件匣提示。
    /// </summary>
    public bool ShowInboxEmptyState => !HasInboxItems;

    /// <summary>
    /// 空收件匣提示文字。
    /// </summary>
    public string InboxEmptyText => "目前沒有通知";

    /// <summary>
    /// 啟用工作區命令。
    /// </summary>
    public ICommand ActivateWorkspaceCommand
        => _activateWorkspaceCommand ??= new RelayCommand(ExecuteActivateWorkspace);

    /// <summary>
    /// 切換工作區選單命令。
    /// </summary>
    public ICommand ToggleWorkspacePopupCommand
        => _toggleWorkspacePopupCommand ??= new RelayCommand(_ => IsWorkspacePopupOpen = !IsWorkspacePopupOpen);

    /// <summary>
    /// 清除收件匣命令。
    /// </summary>
    public ICommand ClearInboxCommand
        => _clearInboxCommand ??= new RelayCommand(_ => InboxItems.Clear());

    /// <summary>
    /// 由 View 載入後呼叫，建立 I/O 訂閱並套用初始快照。
    /// </summary>
    internal void ApplyViewLoaded()
    {
        if (_isInitialized)
        {
            return;
        }

        _synchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext();
        services.WorkspacesChanged += OnWorkspacesChanged;
        services.ModulesChanged += OnModulesChanged;
        services.StateChanged += OnStateChanged;
        services.ModuleHealthChanged += OnShellModuleHealthChanged;
        ModuleNavItems.CollectionChanged += OnModuleNavItemsChanged;
        InboxItems.CollectionChanged += OnInboxItemsChanged;

        ApplyShellSnapshot();
        _isInitialized = true;
    }

    /// <summary>
    /// 釋放 ViewModel 的事件訂閱。
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        if (_isInitialized)
        {
            services.WorkspacesChanged -= OnWorkspacesChanged;
            services.ModulesChanged -= OnModulesChanged;
            services.StateChanged -= OnStateChanged;
            services.ModuleHealthChanged -= OnShellModuleHealthChanged;
            ModuleNavItems.CollectionChanged -= OnModuleNavItemsChanged;
            InboxItems.CollectionChanged -= OnInboxItemsChanged;
        }

        _isDisposed = true;
    }

    private void ExecuteActivateWorkspace(object? parameter)
    {
        if (parameter is not string workspaceName || string.IsNullOrWhiteSpace(workspaceName))
        {
            return;
        }

        _workspaceSwitchToken++;
        _workspaceSwitchStartedTimestamp = Stopwatch.GetTimestamp();

        IsWorkspaceSwitching = true;
        IsModulesLoading = true;
        ActiveWorkspaceName = workspaceName;
        CurrentModuleHealth = "Warning";
        CurrentModuleHealthMessage = "模組切換中";
        IsWorkspacePopupOpen = false;

        ApplyWorkspaceItems(services.QueryWorkspaces(), workspaceName);
        ApplyStatusSnapshot();
        services.ActivateWorkspace(workspaceName);
    }

    private void OnWorkspacesChanged(object? sender, EventArgs e)
        => PostToUi(() =>
        {
            ApplyWorkspaceSnapshot();
            ApplyStatusSnapshot();
        });

    private void OnModulesChanged(object? sender, EventArgs e)
        => PostToUi(() =>
        {
            ApplyModulesSnapshot();
            ApplyStatusSnapshot();
        });

    private void OnStateChanged(object? sender, EventArgs e)
        => PostToUi(ApplyShellSnapshot);

    private void OnModuleNavItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => NotifyPropertyChanged(nameof(HasModuleNavItems));

    private void OnInboxItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotifyPropertyChanged(nameof(UnreadCount));
        NotifyPropertyChanged(nameof(ShowNotificationBadge));
        NotifyPropertyChanged(nameof(HasInboxItems));
        NotifyPropertyChanged(nameof(ShowInboxEmptyState));
    }

    private void PostToUi(Action action)
        => _synchronizationContext.Post(_ => action(), null);

    private void ApplyShellSnapshot()
    {
        ApplyWorkspaceSnapshot();
        ApplyModulesSnapshot();
        ApplyStatusSnapshot();
    }

    private void ApplyWorkspaceSnapshot()
    {
        var workspaceNames = services.QueryWorkspaces();
        var activeWorkspaceName = ResolveActiveWorkspaceName(workspaceNames, services.QueryActiveWorkspaceName());
        ApplyWorkspaceItems(workspaceNames, activeWorkspaceName);
    }

    private void ApplyWorkspaceItems(
        System.Collections.Generic.IReadOnlyList<string> workspaceNames,
        string activeWorkspaceName)
    {
        ActiveWorkspaceName = activeWorkspaceName;
        WorkspaceBreadcrumbText = string.IsNullOrWhiteSpace(activeWorkspaceName) ? "—" : activeWorkspaceName;
        Workspaces.Clear();

        foreach (var workspaceName in workspaceNames)
        {
            Workspaces.Add(new WorkspaceItem
            {
                Name = workspaceName,
                IsActive = string.Equals(workspaceName, activeWorkspaceName, StringComparison.OrdinalIgnoreCase),
            });
        }
    }

    /// <summary>
    /// 套用上方導覽列麵包屑文字。
    /// </summary>
    /// <param name="breadcrumbText">頁面或模組名稱。</param>
    internal void ApplyBreadcrumb(string breadcrumbText)
        => BreadcrumbText = string.IsNullOrWhiteSpace(breadcrumbText) ? "—" : breadcrumbText;

    /// <summary>
    /// 設定目前主內容區顯示的模組識別碼（殼層頁面請傳 null）。
    /// </summary>
    internal void SetContentModuleId(string? moduleId)
    {
        _contentModuleId = moduleId;
        if (moduleId is null)
        {
            CurrentModuleHealth = "Healthy";
            CurrentModuleHealthMessage = "模組狀態正常";
        }
    }

    private void OnShellModuleHealthChanged(string moduleId, ModuleHealth health, string? message)
    {
        if (_contentModuleId is null || !string.Equals(moduleId, _contentModuleId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        PostToUi(() =>
        {
            if (_contentModuleId is null || !string.Equals(moduleId, _contentModuleId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            (CurrentModuleHealth, CurrentModuleHealthMessage) = health switch
            {
                ModuleHealth.Healthy => ("Healthy", message ?? "模組狀態正常"),
                ModuleHealth.Warning => ("Warning", message ?? "模組警告"),
                ModuleHealth.Faulted => ("Faulted", message ?? "模組錯誤"),
                _ => ("Healthy", message ?? string.Empty),
            };
        });
    }

    private void ApplyModulesSnapshot()
    {
        var modules = services.QueryModules();

        ModuleNavItems.Clear();
        foreach (var module in modules)
        {
            ModuleNavItems.Add(new ModuleNavItem
            {
                Tag = module.Id,
                DisplayName = module.DisplayName,
            });
        }

        IsModulesLoading = false;
        ApplyWorkspaceSwitchCompleted();
        CurrentModuleHealth = "Healthy";
        CurrentModuleHealthMessage = modules.Count == 0 ? "目前工作區沒有模組" : "模組狀態正常";
    }

    private async void ApplyWorkspaceSwitchCompleted()
    {
        var token = _workspaceSwitchToken;
        var elapsedMilliseconds = Stopwatch.GetElapsedTime(_workspaceSwitchStartedTimestamp).TotalMilliseconds;
        var remainingMilliseconds = WorkspaceSwitchShimmerMinimumMilliseconds - elapsedMilliseconds;

        if (remainingMilliseconds > 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(remainingMilliseconds));
        }

        if (_workspaceSwitchToken != token)
        {
            return;
        }

        IsWorkspaceSwitching = false;
    }

    private void ApplyStatusSnapshot()
    {
        StatusWorkspaceText = string.IsNullOrWhiteSpace(ActiveWorkspaceName)
            ? "工作區：未選取"
            : $"工作區：{ActiveWorkspaceName}";
        StatusModulesText = $"模組：{ModuleNavItems.Count}";
        StatusDeviceText = $"裝置：{Environment.MachineName}";
        StatusBuildText = $"版本：{Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "未知"}";
    }

    private static string ResolveActiveWorkspaceName(
        System.Collections.Generic.IReadOnlyList<string> workspaceNames,
        string activeWorkspaceName)
    {
        if (workspaceNames.Count == 0)
        {
            return string.Empty;
        }

        return workspaceNames.Contains(activeWorkspaceName, StringComparer.OrdinalIgnoreCase)
            ? activeWorkspaceName
            : workspaceNames[0];
    }

    /// <summary>
    /// 工作區清單項目模型。
    /// </summary>
    public sealed class WorkspaceItem
    {
        /// <summary>
        /// 工作區名稱。
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// 是否為目前啟用的工作區。
        /// </summary>
        public bool IsActive { get; init; }
    }

    /// <summary>
    /// 模組導覽項目模型。
    /// </summary>
    public sealed class ModuleNavItem
    {
        /// <summary>
        /// 模組識別標記。
        /// </summary>
        public string Tag { get; init; } = string.Empty;

        /// <summary>
        /// 模組圖示位元組資料。
        /// </summary>
        public byte[]? Icon { get; init; }

        /// <summary>
        /// 模組顯示名稱。
        /// </summary>
        public string DisplayName { get; init; } = string.Empty;
    }
}
