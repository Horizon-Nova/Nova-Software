using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Helix_Studio.AppHost;
using Helix_Studio.Domain;
using Helix_Studio.Model;
using Helix_Studio.ViewModels.Base;

namespace Helix_Studio.ViewModels;

/// <summary>
/// 主視窗（Shell）ViewModel。
/// </summary>
public sealed class MainWindowWindowViewModel(
    ApplicationRuntime applicationRuntime,
    LocalizationRuntime localizationRuntime,
    NavigationRuntime navigationRuntime,
    EventLogDomain eventLogDomain,
    MainWindowDomain domain) : ViewModelBase, IDisposable
{
    private static readonly TimeSpan WorkspaceSwitchDelay = TimeSpan.FromMilliseconds(1500);
    private const string ProductVersionValue = "v3";

    private bool _isDisposed;
    private bool _isInitialized;
    private bool _suppressSnapshots;

    private RelayCommand<string>? _activateWorkspaceCommand;
    private RelayCommand? _toggleWorkspacePopupCommand;
    private RelayCommand<string>? _navigateCommand;

    private string _activeWorkspaceName = string.Empty;
    private string _activeWorkspaceDescription = string.Empty;
    private bool _isWorkspacePopupOpen;
    private bool _isWorkspaceSwitching;

    private string _currentNavId = "overview";
    private string _statusDeviceText = string.Empty;
    private string _statusBuildText = string.Empty;
    private int _modulesRunningCount;

    private string _workspaceLoadingText = string.Empty;

    public ObservableCollection<WorkspaceItem> Workspaces { get; } = new();

    public string ActiveWorkspaceName
    {
        get => _activeWorkspaceName;
        private set => SetProperty(ref _activeWorkspaceName, value);
    }

    public string ActiveWorkspaceDescription
    {
        get => _activeWorkspaceDescription;
        private set => SetProperty(ref _activeWorkspaceDescription, value);
    }

    public bool IsWorkspacePopupOpen
    {
        get => _isWorkspacePopupOpen;
        set => SetProperty(ref _isWorkspacePopupOpen, value);
    }

    public bool IsWorkspaceSwitching
    {
        get => _isWorkspaceSwitching;
        private set
        {
            if (SetProperty(ref _isWorkspaceSwitching, value))
            {
                NotifyPropertyChanged(nameof(IsWorkspaceLoading));
                ApplyStatusSnapshot();
            }
        }
    }

    public bool IsWorkspaceLoading => IsWorkspaceSwitching;

    public string WorkspaceLoadingText
    {
        get => _workspaceLoadingText;
        private set => SetProperty(ref _workspaceLoadingText, value);
    }

    public string CurrentNavId
    {
        get => _currentNavId;
        private set => SetProperty(ref _currentNavId, value);
    }

    public string ProductVersion => ProductVersionValue;

    public string StatusDeviceText
    {
        get => _statusDeviceText;
        private set => SetProperty(ref _statusDeviceText, value);
    }

    public string StatusBuildText
    {
        get => _statusBuildText;
        private set => SetProperty(ref _statusBuildText, value);
    }

    public int ModulesRunningCount
    {
        get => _modulesRunningCount;
        private set => SetProperty(ref _modulesRunningCount, value);
    }

    public int UnreadCount => 0;

    public bool ShowNotificationBadge => false;

    public RelayCommand ToggleWorkspacePopupCommand
        => _toggleWorkspacePopupCommand ??= new RelayCommand(_ => IsWorkspacePopupOpen = !IsWorkspacePopupOpen);

    public RelayCommand<string> ActivateWorkspaceCommand
        => _activateWorkspaceCommand ??= new RelayCommand<string>(ActivateWorkspace);

    public RelayCommand<string> NavigateCommand
        => _navigateCommand ??= new RelayCommand<string>(Navigate);

    /// <summary>
    /// 由 View 在 Loaded 後呼叫，避免設計階段/DI 建構時誤觸發。
    /// </summary>
    internal void ApplyViewLoaded()
    {
        if (_isInitialized)
        {
            return;
        }

        Workspaces.CollectionChanged += OnWorkspacesChanged;
        domain.StateChanged += OnDomainChanged;
        domain.WorkspacesChanged += OnDomainChanged;
        domain.ModulesChanged += OnDomainChanged;

        ApplySnapshot();
        Navigate(domain.ResolveStartupPageId());
        eventLogDomain.ApplyMaintenance();
        _isInitialized = true;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Workspaces.CollectionChanged -= OnWorkspacesChanged;
        domain.StateChanged -= OnDomainChanged;
        domain.WorkspacesChanged -= OnDomainChanged;
        domain.ModulesChanged -= OnDomainChanged;

        _isDisposed = true;
    }

    private void OnWorkspacesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var item in Workspaces)
        {
            item.IsActive = string.Equals(item.Name, ActiveWorkspaceName, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void OnDomainChanged(object? sender, EventArgs e)
    {
        if (_suppressSnapshots)
        {
            return;
        }

        applicationRuntime.InvokeOnUiThread(() =>
        {
            ApplySnapshot();
            eventLogDomain.ApplyMaintenance();
        });
    }

    private void ApplySnapshot()
    {
        ApplyWorkspaceSnapshot();
        ApplyStatusSnapshot();
    }

    private void ApplyWorkspaceSnapshot()
    {
        var workspaceNames = domain.QueryWorkspaces();
        var active = domain.QueryActiveWorkspaceName();
        ActiveWorkspaceName = ResolveActiveWorkspaceName(workspaceNames, active);

        if (string.IsNullOrWhiteSpace(active) && !string.IsNullOrWhiteSpace(ActiveWorkspaceName))
        {
            domain.ActivateWorkspace(ActiveWorkspaceName);
            ActiveWorkspaceName = domain.QueryActiveWorkspaceName();
        }

        ActiveWorkspaceDescription = domain.QueryActiveWorkspaceDescription();

        Workspaces.Clear();
        foreach (var name in workspaceNames)
        {
            Workspaces.Add(new WorkspaceItem
            {
                Name = name,
                IsActive = string.Equals(name, ActiveWorkspaceName, StringComparison.OrdinalIgnoreCase),
                Description = domain.QueryWorkspaceDescription(name),
            });
        }
    }

    private void ApplyStatusSnapshot()
    {
        ModulesRunningCount = domain.QueryModules().Count;
        StatusDeviceText = Environment.MachineName;
        StatusBuildText = QueryHostVersionDisplay();
    }

    private void Navigate(string? navId)
    {
        var resolved = string.IsNullOrWhiteSpace(navId) ? "overview" : navId;

        CurrentNavId = resolved;
        navigationRuntime.RequestNavigation(resolved);
        domain.SaveLastVisitedPageId(resolved);
    }

    private async void ActivateWorkspace(string? workspaceName)
    {
        if (string.IsNullOrWhiteSpace(workspaceName))
        {
            return;
        }

        IsWorkspacePopupOpen = false;
        WorkspaceLoadingText = localizationRuntime.Format("Loc_Shell_WorkspaceLoading", workspaceName);
        IsWorkspaceSwitching = true;
        _suppressSnapshots = true;

        await Task.Delay(WorkspaceSwitchDelay).ConfigureAwait(true);

        domain.ActivateWorkspace(workspaceName);
        ApplySnapshot();
        _suppressSnapshots = false;
        IsWorkspaceSwitching = false;
    }

    private static string ResolveActiveWorkspaceName(IReadOnlyList<string> workspaceNames, string activeWorkspaceName)
        => workspaceNames.FirstOrDefault(name =>
               string.Equals(name, activeWorkspaceName, StringComparison.OrdinalIgnoreCase))
           ?? workspaceNames.FirstOrDefault()
           ?? string.Empty;

    private string QueryHostVersionDisplay()
    {
        var version = typeof(MainWindowWindowViewModel).Assembly.GetName().Version;
        return version is null ? localizationRuntime.QueryString("Loc_Common_Unknown") : version.ToString();
    }
}

/// <summary>
/// 工作區清單項目（供 Shell 側欄下拉使用）。
/// </summary>
public sealed class WorkspaceItem : ViewModelBase
{
    private string _name = string.Empty;
    private string _description = string.Empty;
    private bool _isActive;

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetField(ref _isActive, value);
    }
}
