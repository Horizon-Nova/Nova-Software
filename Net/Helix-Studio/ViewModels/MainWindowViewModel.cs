using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Helix_Studio.Model;
using Helix_Studio.Model.Notifications;
using Helix_Studio.Services;
using Helix_Studio.ViewModels.Base;

namespace Helix_Studio.ViewModels;

/// <summary>
/// 主視窗 ViewModel。
/// </summary>
public sealed class MainWindowViewModel(MainWindowServices services) : ViewModelBase, IDisposable
{
    private const int WorkspaceSwitchShimmerMinimumMilliseconds = 200;

    private SynchronizationContext _uiContext = new();
    private bool _isInitialized;
    private bool _isDisposed;

    private RelayCommand? _activateWorkspaceCommand;
    private RelayCommand? _toggleWorkspacePopupCommand;
    private RelayCommand? _clearInboxCommand;

    private string _activeWorkspaceName = string.Empty;
    private bool _isWorkspacePopupOpen;
    private bool _isWorkspaceSwitching;
    private bool _isModulesLoading;
    private int _workspaceSwitchToken;
    private long _workspaceSwitchStartedTimestamp;

    private string _workspaceBreadcrumbText = "工作區";
    private string _breadcrumbText = "Home";

    private string _statusWorkspaceText = "工作區：未選擇";
    private string _statusModulesText = "模組：0";
    private string _statusDeviceText = $"裝置：{Environment.MachineName}";
    private string _statusBuildText = $"版本：{Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "未知"}";

    private string _currentModuleHealth = "Healthy";
    private string _currentModuleHealthMessage = "模組狀態正常";
    private string? _contentModuleId;

    /// <summary>
    /// 工作區清單。
    /// </summary>
    public ObservableCollection<WorkspaceItem> Workspaces { get; } = new();

    /// <summary>
    /// 模組導覽清單。
    /// </summary>
    public ObservableCollection<ModuleNavItem> ModuleNavItems { get; } = new();

    /// <summary>
    /// 通知收件匣清單。
    /// </summary>
    public ObservableCollection<ShellInboxItem> InboxItems { get; } = new();

    /// <summary>
    /// 工作區麵包屑文字。
    /// </summary>
    public string WorkspaceBreadcrumbText
    {
        get => _workspaceBreadcrumbText;
        private set => SetField(ref _workspaceBreadcrumbText, value);
    }

    /// <summary>
    /// 麵包屑文字。
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
    /// 是否有模組導覽項目。
    /// </summary>
    public bool HasModuleNavItems => ModuleNavItems.Count > 0;

    /// <summary>
    /// 工作區彈出視窗是否開啟。
    /// </summary>
    public bool IsWorkspacePopupOpen
    {
        get => _isWorkspacePopupOpen;
        set => SetField(ref _isWorkspacePopupOpen, value);
    }

    /// <summary>
    /// 是否正在切換工作區。
    /// </summary>
    public bool IsWorkspaceSwitching
    {
        get => _isWorkspaceSwitching;
        private set => SetField(ref _isWorkspaceSwitching, value);
    }

    /// <summary>
    /// 是否正在載入模組清單。
    /// </summary>
    public bool IsModulesLoading
    {
        get => _isModulesLoading;
        private set => SetField(ref _isModulesLoading, value);
    }

    /// <summary>
    /// 狀態列工作區文字。
    /// </summary>
    public string StatusWorkspaceText
    {
        get => _statusWorkspaceText;
        private set => SetField(ref _statusWorkspaceText, value);
    }

    /// <summary>
    /// 狀態列模組文字。
    /// </summary>
    public string StatusModulesText
    {
        get => _statusModulesText;
        private set => SetField(ref _statusModulesText, value);
    }

    /// <summary>
    /// 狀態列裝置文字。
    /// </summary>
    public string StatusDeviceText
    {
        get => _statusDeviceText;
        private set => SetField(ref _statusDeviceText, value);
    }

    /// <summary>
    /// 狀態列版本文字。
    /// </summary>
    public string StatusBuildText
    {
        get => _statusBuildText;
        private set => SetField(ref _statusBuildText, value);
    }

    /// <summary>
    /// 模組健康狀態字串。
    /// </summary>
    public string CurrentModuleHealth
    {
        get => _currentModuleHealth;
        private set => SetField(ref _currentModuleHealth, value);
    }

    /// <summary>
    /// 模組健康狀態提示文字。
    /// </summary>
    public string CurrentModuleHealthMessage
    {
        get => _currentModuleHealthMessage;
        private set => SetField(ref _currentModuleHealthMessage, value);
    }

    /// <summary>
    /// 是否有通知項目。
    /// </summary>
    public bool HasInboxItems => InboxItems.Count > 0;

    /// <summary>
    /// 是否顯示收件匣空狀態。
    /// </summary>
    public bool ShowInboxEmptyState => !HasInboxItems;

    /// <summary>
    /// 收件匣空狀態文字。
    /// </summary>
    public string InboxEmptyText => "目前沒有通知。";

    /// <summary>
    /// 未讀數量。
    /// </summary>
    public int UnreadCount => InboxItems.Count;

    /// <summary>
    /// 是否顯示通知紅點。
    /// </summary>
    public bool ShowNotificationBadge => UnreadCount > 0;

    /// <summary>
    /// 切換工作區彈出視窗命令。
    /// </summary>
    public ICommand ToggleWorkspacePopupCommand
        => _toggleWorkspacePopupCommand ??= new RelayCommand(() => IsWorkspacePopupOpen = !IsWorkspacePopupOpen);

    /// <summary>
    /// 切換工作區命令。
    /// </summary>
    public ICommand ActivateWorkspaceCommand
        => _activateWorkspaceCommand ??= new RelayCommand(parameter => ExecuteActivateWorkspace(parameter as string));

    /// <summary>
    /// 清除通知命令。
    /// </summary>
    public ICommand ClearInboxCommand
        => _clearInboxCommand ??= new RelayCommand(() => InboxItems.Clear());

    /// <summary>
    /// View 載入後呼叫，建立訂閱並套用初始快照。
    /// </summary>
    public void ApplyViewLoaded()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();

        Workspaces.CollectionChanged += OnWorkspacesChanged;
        ModuleNavItems.CollectionChanged += OnModulesChanged;
        InboxItems.CollectionChanged += OnInboxChanged;

        services.WorkspacesChanged += OnRuntimeChanged;
        services.ModulesChanged += OnRuntimeChanged;
        services.StateChanged += OnRuntimeChanged;
        services.ModuleHealthChanged += OnModuleHealthChanged;

        ApplySnapshot();
    }

    /// <summary>
    /// 套用麵包屑文字。
    /// </summary>
    /// <param name="breadcrumbText">麵包屑文字。</param>
    internal void ApplyBreadcrumb(string breadcrumbText)
        => BreadcrumbText = string.IsNullOrWhiteSpace(breadcrumbText) ? "Home" : breadcrumbText;

    /// <summary>
    /// 設定目前內容模組識別碼。
    /// </summary>
    /// <param name="moduleId">模組識別碼。</param>
    internal void SetContentModuleId(string? moduleId)
    {
        _contentModuleId = moduleId;
        if (moduleId is null)
        {
            CurrentModuleHealth = "Healthy";
            CurrentModuleHealthMessage = "模組狀態正常";
        }
    }

    /// <summary>
    /// 釋放資源。
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        services.WorkspacesChanged -= OnRuntimeChanged;
        services.ModulesChanged -= OnRuntimeChanged;
        services.StateChanged -= OnRuntimeChanged;
        services.ModuleHealthChanged -= OnModuleHealthChanged;

        Workspaces.CollectionChanged -= OnWorkspacesChanged;
        ModuleNavItems.CollectionChanged -= OnModulesChanged;
        InboxItems.CollectionChanged -= OnInboxChanged;
    }

    private void ExecuteActivateWorkspace(string? workspaceName)
    {
        if (string.IsNullOrWhiteSpace(workspaceName))
        {
            return;
        }

        IsWorkspacePopupOpen = false;
        IsWorkspaceSwitching = true;
        IsModulesLoading = true;
        CurrentModuleHealth = "Warning";
        CurrentModuleHealthMessage = "工作區切換中";

        _workspaceSwitchStartedTimestamp = Stopwatch.GetTimestamp();
        _workspaceSwitchToken++;
        var token = _workspaceSwitchToken;

        services.ActivateWorkspace(workspaceName);
        ApplySnapshot();
        _ = ApplyWorkspaceSwitchCompleted(token);
    }

    private async Task ApplyWorkspaceSwitchCompleted(int token)
    {
        var elapsed = Stopwatch.GetElapsedTime(_workspaceSwitchStartedTimestamp).TotalMilliseconds;
        var remaining = WorkspaceSwitchShimmerMinimumMilliseconds - elapsed;

        if (remaining > 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(remaining));
        }

        if (_workspaceSwitchToken != token)
        {
            return;
        }

        IsWorkspaceSwitching = false;
    }

    private void OnRuntimeChanged(object? sender, EventArgs e) => PostToUi(ApplySnapshot);

    private void OnModuleHealthChanged(string moduleId, ModuleHealth health, string? message)
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

    private void ApplySnapshot()
    {
        ApplyWorkspaceSnapshot();
        ApplyModulesSnapshot();
        ApplyStatusSnapshot();
    }

    private void ApplyWorkspaceSnapshot()
    {
        var workspaceNames = services.QueryWorkspaces();
        var active = services.QueryActiveWorkspaceName();
        ActiveWorkspaceName = ResolveActiveWorkspaceName(workspaceNames, active);

        Workspaces.Clear();
        foreach (var name in workspaceNames)
        {
            Workspaces.Add(new WorkspaceItem
            {
                Name = name,
                IsActive = string.Equals(name, ActiveWorkspaceName, StringComparison.OrdinalIgnoreCase),
            });
        }

        WorkspaceBreadcrumbText = string.IsNullOrWhiteSpace(ActiveWorkspaceName) ? "工作區" : ActiveWorkspaceName;
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
        CurrentModuleHealth = "Healthy";
        CurrentModuleHealthMessage = ModuleNavItems.Count == 0 ? "目前工作區沒有模組" : "模組狀態正常";
    }

    private void ApplyStatusSnapshot()
    {
        StatusWorkspaceText = string.IsNullOrWhiteSpace(ActiveWorkspaceName)
            ? "工作區：未選擇"
            : $"工作區：{ActiveWorkspaceName}";
        StatusModulesText = $"模組：{ModuleNavItems.Count}";
        StatusDeviceText = $"裝置：{Environment.MachineName}";
        StatusBuildText = $"版本：{Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "未知"}";

        NotifyPropertyChanged(nameof(ShowNotificationBadge));
        NotifyPropertyChanged(nameof(UnreadCount));
        NotifyPropertyChanged(nameof(HasInboxItems));
        NotifyPropertyChanged(nameof(ShowInboxEmptyState));
    }

    private static string ResolveActiveWorkspaceName(IReadOnlyList<string> workspaceNames, string activeWorkspaceName)
        => workspaceNames.Contains(activeWorkspaceName, StringComparer.OrdinalIgnoreCase)
            ? activeWorkspaceName
            : workspaceNames.FirstOrDefault() ?? string.Empty;

    private void PostToUi(Action action)
        => _uiContext.Post(_ => action(), null);

    private void OnWorkspacesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => NotifyPropertyChanged(nameof(ActiveWorkspaceName));

    private void OnModulesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => NotifyPropertyChanged(nameof(HasModuleNavItems));

    private void OnInboxChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotifyPropertyChanged(nameof(HasInboxItems));
        NotifyPropertyChanged(nameof(ShowInboxEmptyState));
        NotifyPropertyChanged(nameof(ShowNotificationBadge));
        NotifyPropertyChanged(nameof(UnreadCount));
    }

    /// <summary>
    /// 工作區項目。
    /// </summary>
    public sealed class WorkspaceItem
    {
        /// <summary>
        /// 工作區名稱。
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// 是否為啟用中的工作區。
        /// </summary>
        public bool IsActive { get; init; }

        /// <summary>
        /// 描述文字（目前未使用）。
        /// </summary>
        public string? Description { get; init; }
    }

    /// <summary>
    /// 模組導覽項目。
    /// </summary>
    public sealed class ModuleNavItem
    {
        /// <summary>
        /// 導覽標記。
        /// </summary>
        public string Tag { get; init; } = string.Empty;

        /// <summary>
        /// 圖示（PNG 位元組）。
        /// </summary>
        public byte[]? Icon { get; init; }

        /// <summary>
        /// 顯示名稱。
        /// </summary>
        public string DisplayName { get; init; } = string.Empty;
    }
}
