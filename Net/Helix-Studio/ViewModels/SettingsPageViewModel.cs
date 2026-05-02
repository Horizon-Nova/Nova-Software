using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Helix_Studio.Model.Settings;
using Helix_Studio.Model.Workspace;
using Helix_Studio.Services;
using Helix_Studio.ViewModels.Base;

namespace Helix_Studio.ViewModels;

/// <summary>設定分頁的 UI 狀態、搜尋、工作區、模組設定內嵌區與匯出日誌等命令。</summary>
public sealed class SettingsPageViewModel : ViewModelBase, IDisposable
{
    private readonly SettingsServices _settings;
    private readonly ModulesCatalogServices _modulesCatalog;
    private readonly ModuleRuntimeServices _moduleRuntime;

    private bool _loading;
    private bool _isDirty;

    private int _languageIndex;
    private int _startPageIndex;
    private int _inactivityIndex;
    private int _themeIndex;
    private int _fontScaleIndex;
    private int _dataDensityIndex;
    private int _logLevelIndex;
    private int _logRetentionIndex;
    private string _logExportFolder = string.Empty;

    private string _searchQuery = string.Empty;
    private IReadOnlyList<SettingsSearchSuggestion> _searchSuggestions = [];
    private SettingsSearchSuggestion? _selectedSearchSuggestion;
    private bool _isSearchPopupOpen;

    private string _aboutVersionText = "—";
    private string _aboutBuildDateText = "—";
    private string _aboutLicenseExpiryText = "—";

    private IReadOnlyList<ModuleSettingsCard> _moduleSettingsCards = [];

    /// <summary>以應用程式設定、模組目錄與執行時期服務建立，並訂閱清冊變更以更新模組卡。</summary>
    public SettingsPageViewModel(
        SettingsServices settings,
        ModulesCatalogServices modulesCatalog,
        ModuleRuntimeServices moduleRuntime)
    {
        _settings = settings;
        _modulesCatalog = modulesCatalog;
        _moduleRuntime = moduleRuntime;

        LoadedCommand = new RelayCommand(LoadSnapshot);
        SaveCommand = new RelayCommand(SaveSnapshot, () => IsDirty);
        DiscardCommand = new RelayCommand(LoadSnapshot, () => IsDirty);

        SetDataDensityCommand = new RelayCommand(SetDataDensityByParameter);
        RefreshModulesCommand = new RelayCommand(RefreshModuleSettings);
        RequestSectionCommand = new RelayCommand(RequestSectionByParameter);
        AcceptSearchSuggestionCommand = new RelayCommand(AcceptSearchSuggestionByParameter);

        CreateWorkspaceCommand = new RelayCommand(CreateWorkspaceByParameter);
        UpdateWorkspaceCommand = new RelayCommand(UpdateWorkspaceByParameter);
        ActivateWorkspaceCommand = new RelayCommand(ActivateWorkspaceByParameter);
        DeleteWorkspaceCommand = new RelayCommand(DeleteWorkspaceByParameter);

        ExportLogsCommand = new RelayCommand(ExportLogsByParameter);
        OpenLegalNoticesCommand = new RelayCommand(OpenLegalNotices);

        Workspaces.CollectionChanged += (_, _) => RaisePropertyChanged(nameof(ShowWorkspacesEmptyState));

        _moduleRuntime.CatalogChanged += OnCatalogChanged;
    }

    /// <summary>使用 <see cref="AppServices"/> 相關靜態服務的預設建構子。</summary>
    public SettingsPageViewModel()
        : this(AppServices.Settings, AppServices.ModulesCatalog, AppServices.ModuleRuntime) { }

    /// <summary>要求內容區捲動或切換至指定設定區段標籤。</summary>
    public event Action<string>? SectionRequested;

    /// <summary>日誌匯出完成（成功或失敗）時回傳結果給 View。</summary>
    public event Action<LogExportResult>? LogExported;

    /// <summary>嘗試開啟法律聲明檔案後的結果（例如找不到檔案）。</summary>
    public event Action<LegalNoticeResult>? LegalNoticesOpened;

    /// <summary>工作區刪除等操作失敗時的提示訊息（例如僅剩一個工作區）。</summary>
    public event Action<string>? WorkspaceOperationFailed;

    /// <summary>從服務快照載入各欄位、關於資訊與工作區／模組清單。</summary>
    public RelayCommand LoadedCommand { get; }

    /// <summary>將目前欄位寫入持久化（有未儲存變更時可用）。</summary>
    public RelayCommand SaveCommand { get; }

    /// <summary>放棄未儲存變更並重新載入快照。</summary>
    public RelayCommand DiscardCommand { get; }

    /// <summary>參數為資料密度選項索引，寫入 <see cref="DataDensityIndex"/>。</summary>
    public RelayCommand SetDataDensityCommand { get; }

    /// <summary>重新整理模組設定卡清單（掃描或模組狀態變更後）。</summary>
    public RelayCommand RefreshModulesCommand { get; }

    /// <summary>參數為設定區段標籤，觸發 <see cref="SectionRequested"/> 捲動至該區。</summary>
    public RelayCommand RequestSectionCommand { get; }

    /// <summary>參數為 <see cref="SettingsSearchSuggestion"/>，接受建議並導向區段。</summary>
    public RelayCommand AcceptSearchSuggestionCommand { get; }

    /// <summary>參數為 <see cref="WorkspaceModel"/>，建立新工作區。</summary>
    public RelayCommand CreateWorkspaceCommand { get; }

    /// <summary>參數為 <see cref="WorkspaceModel"/>，更新名稱與描述。</summary>
    public RelayCommand UpdateWorkspaceCommand { get; }

    /// <summary>參數為工作區 <c>Id</c>，啟用該工作區。</summary>
    public RelayCommand ActivateWorkspaceCommand { get; }

    /// <summary>參數為工作區 <c>Id</c>，刪除該工作區（受規則限制）。</summary>
    public RelayCommand DeleteWorkspaceCommand { get; }

    /// <summary>參數為匯出目錄路徑，封裝日誌目錄為 ZIP 並觸發 <see cref="LogExported"/>。</summary>
    public RelayCommand ExportLogsCommand { get; }

    /// <summary>在預設程式中開啟第三方聲明，並觸發 <see cref="LegalNoticesOpened"/>。</summary>
    public RelayCommand OpenLegalNoticesCommand { get; }

    /// <summary>相對於最後儲存快照，目前表單是否有未儲存變更。</summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (!SetProperty(ref _isDirty, value))
                return;

            RelayCommand.InvalidateAll();
        }
    }

    /// <summary>介面語言選項索引；變更時標示為有未儲存變更。</summary>
    public int LanguageIndex
    {
        get => _languageIndex;
        set
        {
            if (SetProperty(ref _languageIndex, value))
                MarkDirty();
        }
    }

    /// <summary>啟動時要開啟的頁面（首頁或還原上次分頁等）。</summary>
    public int StartPageIndex
    {
        get => _startPageIndex;
        set
        {
            if (SetProperty(ref _startPageIndex, value))
                MarkDirty();
        }
    }

    /// <summary>閒置逾時或鎖定前等待時間選項索引。</summary>
    public int InactivityIndex
    {
        get => _inactivityIndex;
        set
        {
            if (SetProperty(ref _inactivityIndex, value))
                MarkDirty();
        }
    }

    /// <summary>介面主題（亮／暗等）選項索引。</summary>
    public int ThemeIndex
    {
        get => _themeIndex;
        set
        {
            if (SetProperty(ref _themeIndex, value))
                MarkDirty();
        }
    }

    /// <summary>字型或 UI 縮放選項索引。</summary>
    public int FontScaleIndex
    {
        get => _fontScaleIndex;
        set
        {
            if (SetProperty(ref _fontScaleIndex, value))
                MarkDirty();
        }
    }

    /// <summary>清單與表單資料密度選項索引。</summary>
    public int DataDensityIndex
    {
        get => _dataDensityIndex;
        set
        {
            if (SetProperty(ref _dataDensityIndex, value))
                MarkDirty();
        }
    }

    /// <summary>寫入檔案之日誌最低層級選項索引。</summary>
    public int LogLevelIndex
    {
        get => _logLevelIndex;
        set
        {
            if (SetProperty(ref _logLevelIndex, value))
                MarkDirty();
        }
    }

    /// <summary>日誌檔案在磁碟上保留天數的選項索引。</summary>
    public int LogRetentionIndex
    {
        get => _logRetentionIndex;
        set
        {
            if (SetProperty(ref _logRetentionIndex, value))
                MarkDirty();
        }
    }

    /// <summary>匯出日誌壓縮檔的預設或使用者指定目錄路徑。</summary>
    public string LogExportFolder
    {
        get => _logExportFolder;
        set
        {
            if (SetProperty(ref _logExportFolder, value))
                MarkDirty();
        }
    }

    /// <summary>關於區塊顯示的應用程式版本字串。</summary>
    public string AboutVersionText
    {
        get => _aboutVersionText;
        private set => SetProperty(ref _aboutVersionText, value);
    }

    /// <summary>關於區塊顯示的建置或組建日期字串。</summary>
    public string AboutBuildDateText
    {
        get => _aboutBuildDateText;
        private set => SetProperty(ref _aboutBuildDateText, value);
    }

    /// <summary>關於區塊顯示的授權到期日字串。</summary>
    public string AboutLicenseExpiryText
    {
        get => _aboutLicenseExpiryText;
        private set => SetProperty(ref _aboutLicenseExpiryText, value);
    }

    /// <summary>工作區清單（設定內之工作區一節）。</summary>
    public ObservableCollection<WorkspaceModel> Workspaces { get; } = new();

    /// <summary>是否應顯示「尚無工作區」空狀態。</summary>
    public bool ShowWorkspacesEmptyState => Workspaces.Count == 0;

    /// <summary>可內嵌模組設定內容的模組卡集合。</summary>
    public IReadOnlyList<ModuleSettingsCard> ModuleSettingsCards => _moduleSettingsCards;

    /// <summary>是否沒有任何模組可供設定（空狀態）。</summary>
    public bool ShowModulesEmptyState => _moduleSettingsCards.Count == 0;

    /// <summary>設定頁頂部搜尋關鍵字；變更時更新建議清單。</summary>
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (!SetProperty(ref _searchQuery, value))
                return;

            UpdateSearchSuggestions();
        }
    }

    /// <summary>依 <see cref="SearchQuery"/> 篩選後的搜尋建議。</summary>
    public IReadOnlyList<SettingsSearchSuggestion> SearchSuggestions
    {
        get => _searchSuggestions;
        private set => SetProperty(ref _searchSuggestions, value);
    }

    /// <summary>建議清單中反白或即將採用的一筆建議。</summary>
    public SettingsSearchSuggestion? SelectedSearchSuggestion
    {
        get => _selectedSearchSuggestion;
        set => SetProperty(ref _selectedSearchSuggestion, value);
    }

    /// <summary>是否顯示搜尋建議下拉／快顯內容。</summary>
    public bool IsSearchPopupOpen
    {
        get => _isSearchPopupOpen;
        set => SetProperty(ref _isSearchPopupOpen, value);
    }

    private void MarkDirty()
    {
        if (_loading) return;
        IsDirty = true;
    }

    private void LoadSnapshot()
    {
        _loading = true;

        var s = _settings.QuerySettings();

        LanguageIndex = s.LanguageIndex;
        StartPageIndex = s.StartPageIndex;
        InactivityIndex = s.InactivityIndex;
        ThemeIndex = s.ThemeIndex;
        FontScaleIndex = s.FontScaleIndex;
        DataDensityIndex = s.DataDensityIndex;
        LogLevelIndex = s.LogLevelIndex;
        LogRetentionIndex = s.LogRetentionIndex;

        LogExportFolder = string.IsNullOrWhiteSpace(s.LogExportFolder)
            ? _settings.QueryDefaultLogFolder()
            : s.LogExportFolder;

        LoadAboutInfo();
        RefreshWorkspaces();
        RefreshModuleSettings();

        IsDirty = false;
        _loading = false;
    }

    private void SaveSnapshot()
    {
        var settings = new AppSettings
        {
            LanguageIndex = LanguageIndex,
            StartPageIndex = StartPageIndex,
            InactivityIndex = InactivityIndex,
            ThemeIndex = ThemeIndex,
            FontScaleIndex = FontScaleIndex,
            DataDensityIndex = DataDensityIndex,
            LogLevelIndex = LogLevelIndex,
            LogRetentionIndex = LogRetentionIndex,
            LogExportFolder = LogExportFolder?.Trim() ?? string.Empty,
        };

        _settings.SaveSettings(settings);
        IsDirty = false;
    }

    private void LoadAboutInfo()
    {
        var info = _settings.QueryAboutInfo();
        AboutVersionText = info.VersionText;
        AboutBuildDateText = info.BuildDateText;
        AboutLicenseExpiryText = info.LicenseExpiryText;
    }

    private void RefreshWorkspaces()
    {
        var list = _settings.QueryWorkspaces();

        Workspaces.Clear();
        foreach (var ws in list)
            Workspaces.Add(ws);
    }

    private void CreateWorkspaceByParameter(object? parameter)
    {
        if (parameter is not WorkspaceModel model) return;

        _settings.CreateWorkspace(model.Name, model.Description);
        RefreshWorkspaces();
    }

    private void UpdateWorkspaceByParameter(object? parameter)
    {
        if (parameter is not WorkspaceModel model) return;

        _settings.UpdateWorkspace(model.Id, model.Name, model.Description);
        RefreshWorkspaces();
    }

    private void ActivateWorkspaceByParameter(object? parameter)
    {
        var id = parameter as int?;
        if (!id.HasValue) return;

        _settings.ActivateWorkspace(id.Value);
        RefreshWorkspaces();
    }

    private void DeleteWorkspaceByParameter(object? parameter)
    {
        var id = parameter as int?;
        if (!id.HasValue) return;

        if (!_settings.DeleteWorkspace(id.Value))
        {
            WorkspaceOperationFailed?.Invoke("無法刪除唯一的工作區。");
            return;
        }

        RefreshWorkspaces();
    }

    private void ExportLogsByParameter(object? parameter)
    {
        var exportDirectory = parameter as string ?? string.Empty;
        var result = _settings.ExportLogs(exportDirectory);
        LogExported?.Invoke(result);
    }

    private void OpenLegalNotices()
    {
        var result = _settings.OpenLegalNotices();
        LegalNoticesOpened?.Invoke(result);
    }

    private void SetDataDensityByParameter(object? parameter)
    {
        var index = parameter as int?;
        if (!index.HasValue) return;
        DataDensityIndex = index.Value;
    }

    private void RequestSectionByParameter(object? parameter)
    {
        var tag = parameter as string;
        if (string.IsNullOrWhiteSpace(tag)) return;
        SectionRequested?.Invoke(tag);
    }

    private void AcceptSearchSuggestionByParameter(object? parameter)
    {
        if (parameter is not SettingsSearchSuggestion sug) return;

        IsSearchPopupOpen = false;
        SearchQuery = string.Empty;
        SectionRequested?.Invoke(sug.Tag);
    }

    private static IReadOnlyList<SettingsSearchSuggestion> QuerySearchIndex()
        => new[]
        {
            new SettingsSearchSuggestion("General", "General  一般"),
            new SettingsSearchSuggestion("Display", "Display  顯示"),
            new SettingsSearchSuggestion("Workspaces", "Workspaces  工作區"),
            new SettingsSearchSuggestion("Modules", "Modules  模組設定"),
            new SettingsSearchSuggestion("Log", "Log  日誌"),
            new SettingsSearchSuggestion("About", "About  關於"),
        };

    private void UpdateSearchSuggestions()
    {
        var q = SearchQuery?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(q))
        {
            SearchSuggestions = [];
            SelectedSearchSuggestion = null;
            IsSearchPopupOpen = false;
            return;
        }

        var list = QuerySearchIndex()
            .Where(x => x.Display.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .ToList();

        SearchSuggestions = list;
        SelectedSearchSuggestion = list.Count > 0 ? list[0] : null;
        IsSearchPopupOpen = list.Count > 0;
    }

    private void OnCatalogChanged(object? sender, EventArgs e)
    {
        RefreshModuleSettings();
    }

    private void RefreshModuleSettings()
    {
        var modules = _modulesCatalog.QueryLoadedModules();
        _moduleSettingsCards = modules
            .Select(m =>
            {
                object? settingsContent = null;

                if (_moduleRuntime.IsModuleActivated(m.Id) && _moduleRuntime.ModuleHasSettings(m.Id))
                {
                    settingsContent = _moduleRuntime.ActivateSettings(m.Id);
                }

                 return new ModuleSettingsCard(
                     m.Id,
                     m.DisplayName,
                     m.Version,
                     m.Description,
                     m.Icon,
                     settingsContent);
             })
             .ToList();

        RaisePropertyChanged(nameof(ModuleSettingsCards));
        RaisePropertyChanged(nameof(ShowModulesEmptyState));
    }

    /// <summary>取消訂閱模組目錄變更以停止更新模組卡。</summary>
    public void Dispose()
    {
        _moduleRuntime.CatalogChanged -= OnCatalogChanged;
    }
}

/// <summary>設定頁「模組」區塊中，單一模組的顯示資料與內嵌設定內容。</summary>
/// <param name="Id">模組識別碼。</param>
/// <param name="DisplayName">顯示名稱。</param>
/// <param name="Version">模組版本字串。</param>
/// <param name="Description">模組說明。</param>
/// <param name="Icon">圖示位元組或其他視覺承載，若無則為 <c>null</c>。</param>
/// <param name="SettingsContent">模組提供的設定 UI 內容物件，若無則為 <c>null</c>。</param>
public sealed record ModuleSettingsCard(
    string Id,
    string DisplayName,
    string Version,
    string Description,
    object? Icon,
    object? SettingsContent)
{
    /// <summary>是否具備圖示可供顯示。</summary>
    public bool HasIcon => Icon is not null;

    /// <summary>目前是否已載入可內嵌的模組設定內容。</summary>
    public bool HasSettingsContent => SettingsContent is not null;
}
