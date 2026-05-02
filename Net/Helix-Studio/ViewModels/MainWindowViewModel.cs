using System;
using System.Collections.ObjectModel;
using System.Linq;
using Helix_Studio.Model.Modules;
using Helix_Studio.Model.Navigation;
using Helix_Studio.Model.Notifications;
using Helix_Studio.Model.Settings;
using Helix_Studio.Model.Workspace;
using Helix_Studio.Services;
using Helix_Studio.ViewModels.Base;
using HorizonNova.HelixStudio.ModuleSdk;

namespace Helix_Studio.ViewModels;

/// <summary>主視窗的殼層狀態：工作區、模組導覽、通知匣、狀態列與模組執行委派。</summary>
public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly MainWindowServices _service;
    private readonly SettingsServices _settings;
    private readonly WorkspaceServices _workspace;
    private readonly NotificationsServices _notifications;
    private readonly ModulesCatalogServices _modulesCatalog;
    private readonly ModuleRuntimeServices _moduleRuntime;

    private string _activeWorkspaceName = "—";
    private string _activeWorkspaceDescription = string.Empty;

    private string _statusWorkspaceText = "—";
    private string _statusModulesText = "No modules loaded";
    private string _statusDeviceText = string.Empty;
    private string _statusBuildText = string.Empty;

    private int _unreadCount;
    private string _inboxEmptyText = "暫無通知";

    private ShellModuleActivationOutcome? _lastShellModuleActivation;
    private ModuleManifest? _lastResolvedModuleManifest;
    private object? _lastModuleSettingsView;
    private bool _lastModuleHasSettings;
    private int _globalSearchDistinctTagCount;

    /// <summary>使用 <see cref="AppServices"/> 靜態服務的預設建構子。</summary>
    public MainWindowViewModel()
        : this(
            AppServices.MainWindow,
            AppServices.Settings,
            AppServices.Workspace,
            AppServices.Notifications,
            AppServices.ModulesCatalog,
            AppServices.ModuleRuntime) { }

    /// <summary>以注入的各項服務建立主視窗 ViewModel 並訂閱工作區、通知與模組目錄事件。</summary>
    public MainWindowViewModel(
        MainWindowServices service,
        SettingsServices settings,
        WorkspaceServices workspace,
        NotificationsServices notifications,
        ModulesCatalogServices modulesCatalog,
        ModuleRuntimeServices moduleRuntime)
    {
        _service = service;
        _settings = settings;
        _workspace = workspace;
        _notifications = notifications;
        _modulesCatalog = modulesCatalog;
        _moduleRuntime = moduleRuntime;

        LoadedCommand = new RelayCommand(LoadSnapshot);
        OpenOfflineHelpCommand = new RelayCommand(_service.OpenOfflineHelp);
        ActivateWorkspaceCommand = new RelayCommand(ActivateWorkspaceByParameter);
        MarkAllReadCommand = new RelayCommand(_notifications.MarkAllRead);
        ClearInboxCommand = new RelayCommand(_notifications.ClearInbox);

        PersistLastVisitedCommand = new RelayCommand(o =>
        {
            if (o is string tag)
                _service.PersistLastVisited(tag);
        });

        ActivateModuleCommand = new RelayCommand(o =>
        {
            if (o is not string id || string.IsNullOrWhiteSpace(id))
                return;

            LastShellModuleActivation = _moduleRuntime.Activate(id);
        });

        SaveModuleLicenseKeyCommand = new RelayCommand(o =>
        {
            if (o is not SaveModuleLicensePayload p)
                return;

            _moduleRuntime.SaveModuleLicenseKey(p.ModuleId, p.LicenseKey);
        });

        LogModuleActivationFailedCommand = new RelayCommand(o =>
        {
            if (o is not string id)
                return;

            EventLogServices.Warning($"模組啟用失敗：{id}", "Module");
        });

        ResolveModuleManifestCommand = new RelayCommand(o =>
        {
            if (o is not string id)
                return;

            LastResolvedModuleManifest = _modulesCatalog.GetManifest(id);
        });

        ActivateModuleSettingsCommand = new RelayCommand(o =>
        {
            if (o is not string id)
                return;

            LastModuleSettingsView = _moduleRuntime.ActivateSettings(id);
        });

        RefreshModuleCapabilitiesCommand = new RelayCommand(o =>
        {
            if (o is not string id)
                return;

            LastModuleHasSettings = _moduleRuntime.ModuleHasSettings(id);
        });

        RefreshGlobalSearchCommand = new RelayCommand(o =>
        {
            var q = o as string ?? string.Empty;
            GlobalSearchSuggestions.Clear();
            foreach (var item in _service.QuerySearchSuggestions(q))
                GlobalSearchSuggestions.Add(item);

            GlobalSearchDistinctTagCount = GlobalSearchSuggestions
                .Select(x => x.Tag)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        });

        NavigateFromGlobalSearchCommand = new RelayCommand(o =>
        {
            var q = o as string ?? string.Empty;
            var tag = _service.QueryNavigateTagFromSearch(q);
            GlobalSearchNavigateRequested?.Invoke(tag);
        });

        Workspaces.CollectionChanged += (_, _) => RaisePropertyChanged(nameof(HasWorkspaces));
        ModuleNavItems.CollectionChanged += (_, _) => RaisePropertyChanged(nameof(HasModuleNavItems));
        InboxItems.CollectionChanged += (_, _) =>
        {
            RaisePropertyChanged(nameof(HasInboxItems));
            RaisePropertyChanged(nameof(ShowInboxEmptyState));
        };

        _notifications.InboxChanged += OnInboxChanged;
        _notifications.NotificationPublished += OnNotificationPublished;
        _moduleRuntime.CatalogChanged += OnCatalogChanged;
        _moduleRuntime.ModuleHealthChanged += OnModuleHealthChanged;
        _workspace.WorkspaceActivated += OnWorkspaceActivated;
    }

    /// <summary>轉送殼層內部通知，供主視窗顯示氣泡或飛入提示。</summary>
    public event Action<ShellNotificationMessage>? NotificationPublished;

    /// <summary>模組清冊變更（掃描或卸載）時引發，供內容區更新。</summary>
    public event Action? ModuleCatalogChanged;

    /// <summary>作用中工作區變更且快照已重載時引發。</summary>
    public event Action? WorkspaceActivated;

    /// <summary>初次載入快照完成後，提供應導向的啟動頁標籤。</summary>
    public event Action<string>? InitialNavigationRequested;

    /// <summary>從全域搜尋推斷出導覽標籤時引發（可能為 <c>null</c>）。</summary>
    public event Action<string?>? GlobalSearchNavigateRequested;

    /// <summary>初次載入時重抓狀態列、工作區、模組導覽與通知匣。</summary>
    public RelayCommand LoadedCommand { get; }

    /// <summary>開啟本機離線說明。</summary>
    public RelayCommand OpenOfflineHelpCommand { get; }

    /// <summary>參數為工作區 <c>Id</c>，切換啟用工作區。</summary>
    public RelayCommand ActivateWorkspaceCommand { get; }

    /// <summary>將通知匣內所有項目標為已讀。</summary>
    public RelayCommand MarkAllReadCommand { get; }

    /// <summary>清空通知匣。</summary>
    public RelayCommand ClearInboxCommand { get; }

    /// <summary>參數為導覽標籤字串，寫入「還原上次分頁」之造訪記錄。</summary>
    public RelayCommand PersistLastVisitedCommand { get; }

    /// <summary>參數為模組識別，啟用模組並更新 <see cref="LastShellModuleActivation"/>。</summary>
    public RelayCommand ActivateModuleCommand { get; }

    /// <summary>參數為 <see cref="SaveModuleLicensePayload"/>，持久化模組授權金鑰。</summary>
    public RelayCommand SaveModuleLicenseKeyCommand { get; }

    /// <summary>參數為模組識別，寫入模組啟用失敗之警告日誌。</summary>
    public RelayCommand LogModuleActivationFailedCommand { get; }

    /// <summary>參數為模組識別，更新 <see cref="LastResolvedModuleManifest"/>。</summary>
    public RelayCommand ResolveModuleManifestCommand { get; }

    /// <summary>參數為模組識別，更新 <see cref="LastModuleSettingsView"/>。</summary>
    public RelayCommand ActivateModuleSettingsCommand { get; }

    /// <summary>參數為模組識別，更新 <see cref="LastModuleHasSettings"/>。</summary>
    public RelayCommand RefreshModuleCapabilitiesCommand { get; }

    /// <summary>參數為搜尋字串，重填 <see cref="GlobalSearchSuggestions"/> 與 <see cref="GlobalSearchDistinctTagCount"/>。</summary>
    public RelayCommand RefreshGlobalSearchCommand { get; }

    /// <summary>參數為搜尋字串，依服務推斷標籤並引發 <see cref="GlobalSearchNavigateRequested"/>。</summary>
    public RelayCommand NavigateFromGlobalSearchCommand { get; }

    /// <summary>工作區清單（切換與顯示用）。</summary>
    public ObservableCollection<WorkspaceModel> Workspaces { get; } = new();

    /// <summary>是否至少有一筆工作區。</summary>
    public bool HasWorkspaces => Workspaces.Count > 0;

    /// <summary>已載入模組的側邊導覽項目。</summary>
    public ObservableCollection<ModuleNavItem> ModuleNavItems { get; } = new();

    /// <summary>模組導覽是否非空。</summary>
    public bool HasModuleNavItems => ModuleNavItems.Count > 0;

    /// <summary>依時間新到舊顯示的通知匣項目。</summary>
    public ObservableCollection<ShellInboxItem> InboxItems { get; } = new();

    /// <summary>通知匣是否至少有一筆。</summary>
    public bool HasInboxItems => InboxItems.Count > 0;

    /// <summary>是否應顯示「無通知」空狀態。</summary>
    public bool ShowInboxEmptyState => InboxItems.Count == 0;

    /// <summary>通知抽屜內空狀態的提示字串。</summary>
    public string InboxEmptyText
    {
        get => _inboxEmptyText;
        private set => SetProperty(ref _inboxEmptyText, value);
    }

    /// <summary>狀態列與頂部顯示的啟用中工作區名稱。</summary>
    public string ActiveWorkspaceName
    {
        get => _activeWorkspaceName;
        private set => SetProperty(ref _activeWorkspaceName, value);
    }

    /// <summary>啟用中工作區的說明文字。</summary>
    public string ActiveWorkspaceDescription
    {
        get => _activeWorkspaceDescription;
        private set => SetProperty(ref _activeWorkspaceDescription, value);
    }

    /// <summary>未讀通知筆數；變更時一併更新角標可見性。</summary>
    public int UnreadCount
    {
        get => _unreadCount;
        private set
        {
            if (!SetProperty(ref _unreadCount, value))
                return;

            RaisePropertyChanged(nameof(ShowNotificationBadge));
        }
    }

    /// <summary>是否在通知圖示上顯示未讀角標。</summary>
    public bool ShowNotificationBadge => UnreadCount > 0;

    /// <summary>狀態列上工作區名稱敘述。</summary>
    public string StatusWorkspaceText
    {
        get => _statusWorkspaceText;
        private set => SetProperty(ref _statusWorkspaceText, value);
    }

    /// <summary>狀態列上已載入模組數量敘述。</summary>
    public string StatusModulesText
    {
        get => _statusModulesText;
        private set => SetProperty(ref _statusModulesText, value);
    }

    /// <summary>狀態列上主機名稱。</summary>
    public string StatusDeviceText
    {
        get => _statusDeviceText;
        private set => SetProperty(ref _statusDeviceText, value);
    }

    /// <summary>狀態列上應用程式版本字串。</summary>
    public string StatusBuildText
    {
        get => _statusBuildText;
        private set => SetProperty(ref _statusBuildText, value);
    }

    /// <summary>最近回報的模組整體健康度（導向列或狀態用）。</summary>
    public ModuleHealth CurrentModuleHealth { get; private set; } = ModuleHealth.Healthy;

    /// <summary>與 <see cref="CurrentModuleHealth"/> 搭配顯示的選擇性訊息。</summary>
    public string? CurrentModuleHealthMessage { get; private set; }

    /// <summary>目前主視窗導覽行為參考的使用者設定。</summary>
    public AppSettings CurrentSettings => _service.QueryCurrentSettings();

    /// <summary>依目前閒置選項索引回傳鎖定或逾時間隔。</summary>
    public TimeSpan InactivityLockDuration =>
        _settings.QueryInactivityTimeout(CurrentSettings.InactivityIndex);

    /// <summary>最近一次 <see cref="ActivateModuleCommand"/> 之啟用結果。</summary>
    public ShellModuleActivationOutcome? LastShellModuleActivation
    {
        get => _lastShellModuleActivation;
        private set => SetProperty(ref _lastShellModuleActivation, value);
    }

    /// <summary>最近一次 <see cref="ResolveModuleManifestCommand"/> 解析之清冊。</summary>
    public ModuleManifest? LastResolvedModuleManifest
    {
        get => _lastResolvedModuleManifest;
        private set => SetProperty(ref _lastResolvedModuleManifest, value);
    }

    /// <summary>最近一次 <see cref="ActivateModuleSettingsCommand"/> 之設定檢視物件。</summary>
    public object? LastModuleSettingsView
    {
        get => _lastModuleSettingsView;
        private set => SetProperty(ref _lastModuleSettingsView, value);
    }

    /// <summary>最近一次 <see cref="RefreshModuleCapabilitiesCommand"/> 是否偵測到模組內建設定。</summary>
    public bool LastModuleHasSettings
    {
        get => _lastModuleHasSettings;
        private set => SetProperty(ref _lastModuleHasSettings, value);
    }

    /// <summary>全域搜尋建議清單（供主視窗繫結或指派 <c>ItemsSource</c>）。</summary>
    public ObservableCollection<MainSearchSuggestion> GlobalSearchSuggestions { get; } = new();

    /// <summary>目前搜尋建議中相異導覽標籤數（供角標顯示）。</summary>
    public int GlobalSearchDistinctTagCount
    {
        get => _globalSearchDistinctTagCount;
        private set => SetProperty(ref _globalSearchDistinctTagCount, value);
    }

    private void LoadSnapshot()
    {
        LoadStatus();
        LoadWorkspaces();
        LoadModuleNavItems();
        LoadInbox();
        RaiseInitialNavigation();
    }

    private void RaiseInitialNavigation()
    {
        var supported = _service.QueryShellStaticRouteTags()
            .Concat(ModuleNavItems.Select(x => x.Tag))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var tag = _service.QueryStartPageTag(supported);
        InitialNavigationRequested?.Invoke(tag);
    }

    private void LoadStatus()
    {
        var info = _service.QueryStatusInfo();
        StatusDeviceText = info.DeviceName;
        StatusBuildText = info.VersionText;
        StatusWorkspaceText = info.WorkspaceName;
    }

    private void LoadWorkspaces()
    {
        var list = _service.QueryAllWorkspaces();
        Workspaces.Clear();
        foreach (var ws in list)
            Workspaces.Add(ws);

        var active = list.FirstOrDefault(x => x.IsActive);
        ActiveWorkspaceName = active?.Name ?? "—";
        ActiveWorkspaceDescription = active?.Description ?? string.Empty;
    }

    private void LoadModuleNavItems()
    {
        var manifests = _moduleRuntime.GetManifests();

        ModuleNavItems.Clear();
        foreach (var m in manifests)
        {
            ModuleNavItems.Add(
                new ModuleNavItem
                {
                    Tag = $"Module:{m.Id}",
                    DisplayName = m.DisplayName,
                    Icon = m.IconPngBytes,
                });
        }

        StatusModulesText = manifests.Count == 0
            ? "No modules loaded"
            : $"{manifests.Count} module{(manifests.Count > 1 ? "s" : "")} loaded";
    }

    private void LoadInbox()
    {
        var items = _notifications.QueryInboxNewestFirst();

        InboxItems.Clear();
        foreach (var item in items)
            InboxItems.Add(item);

        UnreadCount = _notifications.QueryUnreadCount();
        InboxEmptyText = InboxItems.Count == 0 ? "暫無通知" : string.Empty;
    }

    private void ActivateWorkspaceByParameter(object? parameter)
    {
        var id = parameter as int?;
        if (!id.HasValue) return;

        _service.ActivateWorkspace(id.Value);
        LoadWorkspaces();
        LoadModuleNavItems();
        LoadStatus();
    }

    private void OnInboxChanged()
    {
        LoadInbox();
    }

    private void OnNotificationPublished(ShellNotificationMessage message)
    {
        NotificationPublished?.Invoke(message);
    }

    private void OnCatalogChanged(object? sender, EventArgs e)
    {
        LoadModuleNavItems();
        ModuleCatalogChanged?.Invoke();
    }

    private void OnModuleHealthChanged(string moduleId, ModuleHealth health, string? message)
    {
        CurrentModuleHealth = health;
        CurrentModuleHealthMessage = message;
        RaisePropertyChanged(nameof(CurrentModuleHealth));
        RaisePropertyChanged(nameof(CurrentModuleHealthMessage));
    }

    private void OnWorkspaceActivated()
    {
        LoadWorkspaces();
        LoadModuleNavItems();
        LoadStatus();
        WorkspaceActivated?.Invoke();
    }

    /// <summary>取消各服務與執行時期事件之訂閱。</summary>
    public void Dispose()
    {
        _notifications.InboxChanged -= OnInboxChanged;
        _notifications.NotificationPublished -= OnNotificationPublished;
        _moduleRuntime.CatalogChanged -= OnCatalogChanged;
        _moduleRuntime.ModuleHealthChanged -= OnModuleHealthChanged;
        _workspace.WorkspaceActivated -= OnWorkspaceActivated;
    }
}
