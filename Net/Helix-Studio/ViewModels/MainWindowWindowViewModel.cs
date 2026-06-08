using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Helix_Studio.AppRuntime;
using Helix_Studio.Domain;
using Helix_Studio.Infrastructure.Help;
using Helix_Studio.Model;
using Helix_Studio.ViewModels.Base;

namespace Helix_Studio.ViewModels;

/// <summary>
/// 主視窗（Shell）ViewModel。
/// </summary>
public sealed class MainWindowWindowViewModel(
    ApplicationRuntime applicationRuntime,
    LocalizationRuntime localizationRuntime,
    NotificationRuntime notificationRuntime,
    NavigationRuntime navigationRuntime,
    EventLogDomain eventLogDomain,
    MainWindowDomain domain) : ViewModelBase, IDisposable
{
    private const string ModulePagePrefix = "module:";
    private const string ModuleSettingsPagePrefix = "module-settings:";
    private static readonly TimeSpan WorkspaceSwitchDelay = TimeSpan.FromMilliseconds(1500);
    private const string ProductVersionValue = "v3";

    private bool _isDisposed;
    private bool _isInitialized;
    private bool _suppressSnapshots;

    private RelayCommand<string>? _activateWorkspaceCommand;
    private RelayCommand? _toggleWorkspacePopupCommand;
    private RelayCommand<string>? _navigateCommand;
    private RelayCommand? _openNotificationsCommand;
    private RelayCommand? _openHelpCommand;
    private RelayCommand? _openWorkspaceSettingsCommand;

    private string _activeWorkspaceName = string.Empty;
    private string _activeWorkspaceDescription = string.Empty;
    private bool _isWorkspacePopupOpen;
    private bool _isWorkspaceSwitching;

    private string _currentNavId = "overview";
    private string _currentSectionTitle = string.Empty;
    private string _currentPageTitle = string.Empty;
    private string _statusDeviceText = string.Empty;
    private string _statusBuildText = string.Empty;
    private int _modulesRunningCount;

    private string _workspaceLoadingText = string.Empty;

    /// <summary>
    /// 可供切換的工作區清單。
    /// </summary>
    public ObservableCollection<WorkspaceItem> Workspaces { get; } = new();

    /// <summary>
    /// 左側模組導覽項目。
    /// </summary>
    public ObservableCollection<ModuleNavigationItem> ModuleNavigationItems { get; } = new();

    /// <summary>
    /// 目前作用中的工作區名稱。
    /// </summary>
    public string ActiveWorkspaceName
    {
        get => _activeWorkspaceName;
        private set => SetProperty(ref _activeWorkspaceName, value);
    }

    /// <summary>
    /// 目前作用中的工作區描述。
    /// </summary>
    public string ActiveWorkspaceDescription
    {
        get => _activeWorkspaceDescription;
        private set => SetProperty(ref _activeWorkspaceDescription, value);
    }

    /// <summary>
    /// 是否開啟工作區下拉面板。
    /// </summary>
    public bool IsWorkspacePopupOpen
    {
        get => _isWorkspacePopupOpen;
        set => SetProperty(ref _isWorkspacePopupOpen, value);
    }

    /// <summary>
    /// 是否正在切換工作區。
    /// </summary>
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

    /// <summary>
    /// 是否顯示工作區切換中的載入狀態。
    /// </summary>
    public bool IsWorkspaceLoading => IsWorkspaceSwitching;

    /// <summary>
    /// 工作區切換時顯示的載入文字。
    /// </summary>
    public string WorkspaceLoadingText
    {
        get => _workspaceLoadingText;
        private set => SetProperty(ref _workspaceLoadingText, value);
    }

    /// <summary>
    /// 目前主內容區頁面代號。
    /// </summary>
    public string CurrentNavId
    {
        get => _currentNavId;
        private set
        {
            if (!SetProperty(ref _currentNavId, value))
            {
                return;
            }

            ApplyModuleNavigationSelection();
            ApplyCurrentPageTitles();
        }
    }

    /// <summary>
    /// 目前頁面所屬區段標題。
    /// </summary>
    public string CurrentSectionTitle
    {
        get => _currentSectionTitle;
        private set => SetProperty(ref _currentSectionTitle, value);
    }

    /// <summary>
    /// 目前頁面標題。
    /// </summary>
    public string CurrentPageTitle
    {
        get => _currentPageTitle;
        private set => SetProperty(ref _currentPageTitle, value);
    }

    /// <summary>
    /// 產品版本標記文字。
    /// </summary>
    public string ProductVersion => ProductVersionValue;

    /// <summary>
    /// 狀態列中的裝置資訊文字。
    /// </summary>
    public string StatusDeviceText
    {
        get => _statusDeviceText;
        private set => SetProperty(ref _statusDeviceText, value);
    }

    /// <summary>
    /// 狀態列中的版本資訊文字。
    /// </summary>
    public string StatusBuildText
    {
        get => _statusBuildText;
        private set => SetProperty(ref _statusBuildText, value);
    }

    /// <summary>
    /// 目前執行中的模組數量。
    /// </summary>
    public int ModulesRunningCount
    {
        get => _modulesRunningCount;
        private set => SetProperty(ref _modulesRunningCount, value);
    }

    /// <summary>
    /// 未讀通知數量。
    /// </summary>
    public int UnreadCount => 0;

    /// <summary>
    /// 是否顯示通知徽章。
    /// </summary>
    public bool ShowNotificationBadge => false;

    /// <summary>
    /// 是否顯示左側模組導覽區。
    /// </summary>
    public bool ShowModuleNavigation => ModuleNavigationItems.Count > 0;

    /// <summary>
    /// 切換工作區面板開關命令。
    /// </summary>
    public RelayCommand ToggleWorkspacePopupCommand
        => _toggleWorkspacePopupCommand ??= new RelayCommand(_ => IsWorkspacePopupOpen = !IsWorkspacePopupOpen);

    /// <summary>
    /// 啟用指定工作區的命令。
    /// </summary>
    public RelayCommand<string> ActivateWorkspaceCommand
        => _activateWorkspaceCommand ??= new RelayCommand<string>(ActivateWorkspace);

    /// <summary>
    /// 切換主內容頁面的命令。
    /// </summary>
    public RelayCommand<string> NavigateCommand
        => _navigateCommand ??= new RelayCommand<string>(parameter => Navigate(parameter));

    /// <summary>
    /// ?鈴鐺?賭誘??    /// </summary>
    public RelayCommand OpenNotificationsCommand
        => _openNotificationsCommand ??= new RelayCommand(_ => Navigate("eventlog"));

    /// <summary>
    /// ?說明?賭誘??    /// </summary>
    public RelayCommand OpenHelpCommand
        => _openHelpCommand ??= new RelayCommand(_ => OpenHelp());

    /// <summary>
    /// ??工作區設定?賭誘??    /// </summary>
    public RelayCommand OpenWorkspaceSettingsCommand
        => _openWorkspaceSettingsCommand ??= new RelayCommand(_ => OpenWorkspaceSettings());

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
        ModuleNavigationItems.CollectionChanged += OnModuleNavigationItemsChanged;
        domain.StateChanged += OnDomainChanged;
        domain.WorkspacesChanged += OnDomainChanged;
        domain.ModulesChanged += OnDomainChanged;

        ApplySnapshot();
        Navigate(domain.ResolveStartupPageId());
        eventLogDomain.ApplyMaintenance();
        _isInitialized = true;
    }

    /// <summary>
    /// 釋放事件訂閱與 ViewModel 資源。
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Workspaces.CollectionChanged -= OnWorkspacesChanged;
        ModuleNavigationItems.CollectionChanged -= OnModuleNavigationItemsChanged;
        domain.StateChanged -= OnDomainChanged;
        domain.WorkspacesChanged -= OnDomainChanged;
        domain.ModulesChanged -= OnDomainChanged;

        _isDisposed = true;
    }

    /// <summary>
    /// 供 View code-behind 要求 Shell 導向指定頁面與目標區段或模組。
    /// </summary>
    /// <param name="pageId">目標頁面識別碼。</param>
    /// <param name="moduleId">可選的模組識別碼或設定區段標籤。</param>
    internal void ApplyNavigationRequest(string pageId, string? moduleId = null)
        => Navigate(pageId, moduleId);

    internal static string BuildModuleNavigationId(string moduleId)
        => $"{ModulePagePrefix}{moduleId}";

    internal static string BuildModuleSettingsNavigationId(string moduleId)
        => $"{ModuleSettingsPagePrefix}{moduleId}";

    private void OnWorkspacesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var item in Workspaces)
        {
            item.IsActive = string.Equals(item.Name, ActiveWorkspaceName, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void OnModuleNavigationItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => NotifyPropertyChanged(nameof(ShowModuleNavigation));

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
        ApplyModuleNavigationSnapshot();
        ApplyStatusSnapshot();
    }

    private void ApplyWorkspaceSnapshot()
    {
        var workspaceNames = domain.QueryWorkspaces();
        var active = domain.QueryActiveWorkspaceName();
        var displayActiveWorkspaceName = ResolveActiveWorkspaceName(workspaceNames, active);
        ActiveWorkspaceName = displayActiveWorkspaceName;
        ActiveWorkspaceDescription = string.IsNullOrWhiteSpace(displayActiveWorkspaceName)
            ? string.Empty
            : domain.QueryWorkspaceDescription(displayActiveWorkspaceName);

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

    private void Navigate(string? pageId, string? moduleId = null)
    {
        var resolved = string.IsNullOrWhiteSpace(pageId) ? "overview" : pageId;

        CurrentNavId = resolved;
        navigationRuntime.RequestNavigation(resolved, moduleId);
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
    {
        var resolved = workspaceNames.FirstOrDefault(name =>
            string.Equals(name, activeWorkspaceName, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(resolved))
        {
            return resolved;
        }

        if (string.IsNullOrWhiteSpace(activeWorkspaceName))
        {
            return workspaceNames.FirstOrDefault() ?? string.Empty;
        }

        return activeWorkspaceName;
    }

    private void OpenHelp()
    {
        try
        {
            HelpCenterLauncher.OpenIndex();
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex);
            notificationRuntime.Request(new NotificationRequest
            {
                Title = localizationRuntime.QueryString("Loc_Shell_Tooltip_Help"),
                Message = ex.Message,
                Severity = NotificationSeverity.Error,
            });
        }
    }

    private void OpenWorkspaceSettings()
    {
        IsWorkspacePopupOpen = false;
        Navigate("settings", "Workspaces");
    }

    private void ApplyModuleNavigationSnapshot()
    {
        var modules = domain.QueryModules()
            .Select(module => new ModuleNavigationItem
            {
                Id = module.Id,
                DisplayName = module.DisplayName,
                NavigationId = BuildModuleNavigationId(module.Id),
            })
            .ToList();

        ModuleNavigationItems.Clear();
        foreach (var item in modules)
        {
            item.IsActive = string.Equals(item.NavigationId, CurrentNavId, StringComparison.OrdinalIgnoreCase);
            ModuleNavigationItems.Add(item);
        }
    }

    private void ApplyModuleNavigationSelection()
    {
        foreach (var item in ModuleNavigationItems)
        {
            item.IsActive =
                string.Equals(item.NavigationId, CurrentNavId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(BuildModuleSettingsNavigationId(item.Id), CurrentNavId, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void ApplyCurrentPageTitles()
    {
        if (CurrentNavId.StartsWith(ModulePagePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var moduleId = CurrentNavId[ModulePagePrefix.Length..];
            var module = ModuleNavigationItems.FirstOrDefault(item => string.Equals(item.Id, moduleId, StringComparison.OrdinalIgnoreCase));
            CurrentSectionTitle = localizationRuntime.QueryString("Loc_Activity_Modules");
            CurrentPageTitle = module?.DisplayName ?? moduleId;
            return;
        }

        if (CurrentNavId.StartsWith(ModuleSettingsPagePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var moduleId = CurrentNavId[ModuleSettingsPagePrefix.Length..];
            var module = ModuleNavigationItems.FirstOrDefault(item => string.Equals(item.Id, moduleId, StringComparison.OrdinalIgnoreCase));
            CurrentSectionTitle = localizationRuntime.QueryString("Loc_Activity_Modules");
            CurrentPageTitle = $"{module?.DisplayName ?? moduleId} 設定";
            return;
        }

        CurrentSectionTitle = CurrentNavId switch
        {
            "overview" => localizationRuntime.QueryString("Loc_ShellTab_Home"),
            "eventlog" => localizationRuntime.QueryString("Loc_Activity_EventLog"),
            "modules" => localizationRuntime.QueryString("Loc_Activity_Modules"),
            "settings" => localizationRuntime.QueryString("Loc_Activity_Settings"),
            _ => CurrentNavId,
        };

        CurrentPageTitle = CurrentSectionTitle;
    }

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

    /// <summary>
    /// Shell 工作區清單項目的名稱。
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    /// <summary>
    /// Shell 工作區清單項目的描述。
    /// </summary>
    public string Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    /// <summary>
    /// 是否為目前作用中的工作區。
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set => SetField(ref _isActive, value);
    }
}

/// <summary>
/// 左側模組導覽項目 ViewModel。
/// </summary>
public sealed class ModuleNavigationItem : ViewModelBase
{
    private string _id = string.Empty;
    private string _displayName = string.Empty;
    private string _navigationId = string.Empty;
    private bool _isActive;

    /// <summary>
    /// 模組識別碼。
    /// </summary>
    public string Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    /// <summary>
    /// 模組顯示名稱。
    /// </summary>
    public string DisplayName
    {
        get => _displayName;
        set => SetField(ref _displayName, value);
    }

    /// <summary>
    /// 導覽用 page id。
    /// </summary>
    public string NavigationId
    {
        get => _navigationId;
        set => SetField(ref _navigationId, value);
    }

    /// <summary>
    /// 是否為目前作用中的導覽項目。
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set => SetField(ref _isActive, value);
    }
}
