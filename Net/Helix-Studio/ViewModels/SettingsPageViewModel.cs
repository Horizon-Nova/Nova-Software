using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using Helix_Studio.Model;
using Helix_Studio.Services;
using Helix_Studio.ViewModels.Base;

namespace Helix_Studio.ViewModels;

/// <summary>
/// 設定頁面的 UI 狀態機。
/// </summary>
public sealed class SettingsPageViewModel(SettingsServices services) : ViewModelBase, IDisposable
{
    private static readonly SettingsSearchSuggestionModel[] SectionSuggestions =
    [
        new() { SectionTag = "General", Display = "General 一般" },
        new() { SectionTag = "Display", Display = "Display 顯示" },
        new() { SectionTag = "Workspaces", Display = "Workspaces 工作區" },
        new() { SectionTag = "Modules", Display = "Modules 模組設定" },
        new() { SectionTag = "Log", Display = "Log 日誌設定" },
        new() { SectionTag = "About", Display = "About 關於" },
    ];

    private readonly ObservableCollection<SettingsSearchSuggestionModel> _searchSuggestions = [];
    private readonly ObservableCollection<SettingsWorkspaceItemModel> _workspaces = [];
    private readonly ObservableCollection<SettingsModuleCardModel> _moduleSettingsCards = [];
    private SynchronizationContext _synchronizationContext = new();
    private RelayCommand? _reloadCommand;
    private RelayCommand? _saveCommand;
    private RelayCommand? _discardCommand;
    private RelayCommand? _activateWorkspaceCommand;
    private RelayCommand? _deleteWorkspaceCommand;
    private RelayCommand? _exportLogsCommand;
    private RelayCommand? _openLegalNoticesCommand;
    private bool _isInitialized;
    private bool _isDisposed;
    private bool _isApplyingSnapshot;
    private string _searchQuery = string.Empty;
    private bool _isSearchPopupOpen;
    private SettingsSearchSuggestionModel? _selectedSearchSuggestion;
    private bool _isDirty;
    private int _dataDensityIndex;
    private int _fontScaleIndex;
    private int _inactivityIndex;
    private int _languageIndex;
    private string _lastVisitedPageTag = string.Empty;
    private string _logExportFolder = string.Empty;
    private int _logLevelIndex;
    private int _logRetentionIndex;
    private int _startPageIndex;
    private int _themeIndex;
    private string _aboutVersionText = "—";
    private string _aboutBuildDateText = "—";
    private string _aboutLicenseExpiryText = "—";

    /// <summary>
    /// 搜尋文字。
    /// </summary>
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (!SetField(ref _searchQuery, value))
            {
                return;
            }

            ApplySearchSuggestions();
        }
    }

    /// <summary>
    /// 是否開啟搜尋建議清單。
    /// </summary>
    public bool IsSearchPopupOpen
    {
        get => _isSearchPopupOpen;
        set => SetField(ref _isSearchPopupOpen, value);
    }

    /// <summary>
    /// 搜尋建議清單。
    /// </summary>
    public ObservableCollection<SettingsSearchSuggestionModel> SearchSuggestions => _searchSuggestions;

    /// <summary>
    /// 目前選中的搜尋建議。
    /// </summary>
    public SettingsSearchSuggestionModel? SelectedSearchSuggestion
    {
        get => _selectedSearchSuggestion;
        set => SetField(ref _selectedSearchSuggestion, value);
    }

    /// <summary>
    /// 使用者是否已修改但尚未儲存。
    /// </summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (!SetField(ref _isDirty, value))
            {
                return;
            }

            _saveCommand?.NotifyCanExecuteChanged();
            _discardCommand?.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// 設定頁面的工作區卡片清單。
    /// </summary>
    public ObservableCollection<SettingsWorkspaceItemModel> Workspaces => _workspaces;

    /// <summary>
    /// 設定頁面的模組卡片清單。
    /// </summary>
    public ObservableCollection<SettingsModuleCardModel> ModuleSettingsCards => _moduleSettingsCards;

    /// <summary>
    /// 是否顯示模組空狀態。
    /// </summary>
    public bool ShowModulesEmptyState => ModuleSettingsCards.Count == 0;

    /// <summary>
    /// 是否顯示工作區空狀態。
    /// </summary>
    public bool ShowWorkspacesEmptyState => Workspaces.Count == 0;

    /// <summary>
    /// 語言選單索引。
    /// </summary>
    public int LanguageIndex
    {
        get => _languageIndex;
        set => SetSettingsField(ref _languageIndex, value);
    }

    /// <summary>
    /// 起始頁面選單索引。
    /// </summary>
    public int StartPageIndex
    {
        get => _startPageIndex;
        set => SetSettingsField(ref _startPageIndex, value);
    }

    /// <summary>
    /// 非活躍逾時選單索引。
    /// </summary>
    public int InactivityIndex
    {
        get => _inactivityIndex;
        set => SetSettingsField(ref _inactivityIndex, value);
    }

    /// <summary>
    /// 佈景主題選單索引。
    /// </summary>
    public int ThemeIndex
    {
        get => _themeIndex;
        set => SetSettingsField(ref _themeIndex, value);
    }

    /// <summary>
    /// 字型縮放選單索引。
    /// </summary>
    public int FontScaleIndex
    {
        get => _fontScaleIndex;
        set => SetSettingsField(ref _fontScaleIndex, value);
    }

    /// <summary>
    /// 資料密度選單索引。
    /// </summary>
    public int DataDensityIndex
    {
        get => _dataDensityIndex;
        set => SetSettingsField(ref _dataDensityIndex, value);
    }

    /// <summary>
    /// 日誌等級選單索引。
    /// </summary>
    public int LogLevelIndex
    {
        get => _logLevelIndex;
        set => SetSettingsField(ref _logLevelIndex, value);
    }

    /// <summary>
    /// 日誌保留天數選單索引。
    /// </summary>
    public int LogRetentionIndex
    {
        get => _logRetentionIndex;
        set => SetSettingsField(ref _logRetentionIndex, value);
    }

    /// <summary>
    /// 日誌匯出資料夾。
    /// </summary>
    public string LogExportFolder
    {
        get => _logExportFolder;
        set => SetSettingsField(ref _logExportFolder, value);
    }

    /// <summary>
    /// 關於頁版本文字。
    /// </summary>
    public string AboutVersionText
    {
        get => _aboutVersionText;
        private set => SetField(ref _aboutVersionText, value);
    }

    /// <summary>
    /// 關於頁建置日期文字。
    /// </summary>
    public string AboutBuildDateText
    {
        get => _aboutBuildDateText;
        private set => SetField(ref _aboutBuildDateText, value);
    }

    /// <summary>
    /// 關於頁授權到期日文字。
    /// </summary>
    public string AboutLicenseExpiryText
    {
        get => _aboutLicenseExpiryText;
        private set => SetField(ref _aboutLicenseExpiryText, value);
    }

    /// <summary>
    /// 重新套用來源快照。
    /// </summary>
    public ICommand ReloadCommand
        => _reloadCommand ??= new RelayCommand(_ => ApplySettingsSnapshot(services.QuerySettings(), false));

    /// <summary>
    /// 儲存設定值。
    /// </summary>
    public ICommand SaveCommand
        => _saveCommand ??= new RelayCommand(_ => ApplySave(), _ => IsDirty);

    /// <summary>
    /// 放棄未儲存變更。
    /// </summary>
    public ICommand DiscardCommand
        => _discardCommand ??= new RelayCommand(_ => ApplySettingsSnapshot(services.QuerySettings(), false), _ => IsDirty);

    /// <summary>
    /// 切換目前作用中的工作區。
    /// </summary>
    public ICommand ActivateWorkspaceCommand
        => _activateWorkspaceCommand ??= new RelayCommand(parameter => services.ActivateWorkspace(parameter?.ToString() ?? string.Empty));

    /// <summary>
    /// 刪除指定工作區。
    /// </summary>
    public ICommand DeleteWorkspaceCommand
        => _deleteWorkspaceCommand ??= new RelayCommand(parameter => services.DeleteWorkspace(parameter?.ToString() ?? string.Empty));

    /// <summary>
    /// 匯出目前日誌壓縮檔。
    /// </summary>
    public ICommand ExportLogsCommand
        => _exportLogsCommand ??= new RelayCommand(parameter => services.ExportLogs(parameter?.ToString() ?? string.Empty));

    /// <summary>
    /// 開啟法律文件。
    /// </summary>
    public ICommand OpenLegalNoticesCommand
        => _openLegalNoticesCommand ??= new RelayCommand(_ => services.OpenLegalNotices());

    /// <summary>
    /// 初始化設定頁面的 I/O 訂閱與初始快照。
    /// </summary>
    internal void ApplyViewLoaded()
    {
        if (_isInitialized)
        {
            return;
        }

        _synchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext();
        services.SettingsChanged += OnSettingsChanged;
        services.WorkspacesChanged += OnWorkspacesChanged;
        services.ModulesChanged += OnModulesChanged;
        Workspaces.CollectionChanged += OnWorkspacesCollectionChanged;
        ModuleSettingsCards.CollectionChanged += OnModuleSettingsCardsCollectionChanged;

        ApplySettingsSnapshot(services.QuerySettings(), false);
        ApplyWorkspacesSnapshot();
        ApplyModuleSettingsCardsSnapshot();
        ApplyAboutSnapshot();
        _isInitialized = true;
    }

    /// <summary>
    /// 套用搜尋建議後回傳對應區段標籤。
    /// </summary>
    internal string QuerySelectedSectionTag()
    {
        var sectionTag = SelectedSearchSuggestion?.SectionTag;
        return string.IsNullOrWhiteSpace(sectionTag)
            ? SectionSuggestions.FirstOrDefault(item => string.Equals(item.Display, SearchQuery, StringComparison.OrdinalIgnoreCase))?.SectionTag ?? string.Empty
            : sectionTag;
    }

    /// <summary>
    /// 套用目前選中的搜尋建議。
    /// </summary>
    internal void ApplySelectedSearchSuggestion()
    {
        if (SelectedSearchSuggestion is null)
        {
            return;
        }

        SearchQuery = SelectedSearchSuggestion.Display;
        IsSearchPopupOpen = false;
    }

    /// <summary>
    /// 套用使用者選取的日誌匯出資料夾。
    /// </summary>
    /// <param name="folderPath">使用者選取的資料夾路徑。</param>
    internal void ApplyLogExportFolder(string folderPath)
        => LogExportFolder = folderPath;

    /// <summary>
    /// 釋放設定頁面的事件訂閱。
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        if (_isInitialized)
        {
            services.SettingsChanged -= OnSettingsChanged;
            services.WorkspacesChanged -= OnWorkspacesChanged;
            services.ModulesChanged -= OnModulesChanged;
            Workspaces.CollectionChanged -= OnWorkspacesCollectionChanged;
            ModuleSettingsCards.CollectionChanged -= OnModuleSettingsCardsCollectionChanged;
        }

        _isDisposed = true;
    }

    private void ApplySave()
    {
        services.SaveSettings(new SettingsModel
        {
            DataDensityIndex = DataDensityIndex,
            FontScaleIndex = FontScaleIndex,
            InactivityIndex = InactivityIndex,
            LanguageIndex = LanguageIndex,
            LastVisitedPageTag = _lastVisitedPageTag,
            LogExportFolder = LogExportFolder,
            LogLevelIndex = LogLevelIndex,
            LogRetentionIndex = LogRetentionIndex,
            StartPageIndex = StartPageIndex,
            ThemeIndex = ThemeIndex,
        });

        IsDirty = false;
    }

    private void ApplySettingsSnapshot(SettingsModel settings, bool preserveDirty)
    {
        if (preserveDirty && IsDirty)
        {
            return;
        }

        _isApplyingSnapshot = true;
        DataDensityIndex = settings.DataDensityIndex;
        FontScaleIndex = settings.FontScaleIndex;
        InactivityIndex = settings.InactivityIndex;
        LanguageIndex = settings.LanguageIndex;
        _lastVisitedPageTag = settings.LastVisitedPageTag;
        LogExportFolder = settings.LogExportFolder;
        LogLevelIndex = settings.LogLevelIndex;
        LogRetentionIndex = settings.LogRetentionIndex;
        StartPageIndex = settings.StartPageIndex;
        ThemeIndex = settings.ThemeIndex;
        _isApplyingSnapshot = false;
        IsDirty = false;
    }

    private void ApplySearchSuggestions()
    {
        SearchSuggestions.Clear();

        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            IsSearchPopupOpen = false;
            SelectedSearchSuggestion = null;
            return;
        }

        foreach (var suggestion in SectionSuggestions.Where(item => item.Display.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)))
        {
            SearchSuggestions.Add(suggestion);
        }

        SelectedSearchSuggestion = SearchSuggestions.FirstOrDefault();
        IsSearchPopupOpen = SearchSuggestions.Count > 0;
    }

    private void ApplyWorkspacesSnapshot()
    {
        Workspaces.Clear();
        foreach (var workspace in services.QueryWorkspaces())
        {
            Workspaces.Add(workspace);
        }
    }

    private void ApplyModuleSettingsCardsSnapshot()
    {
        ModuleSettingsCards.Clear();
        foreach (var moduleCard in services.QueryModuleSettingsCards())
        {
            ModuleSettingsCards.Add(moduleCard);
        }
    }

    private void ApplyAboutSnapshot()
    {
        var about = services.QueryAbout();
        AboutVersionText = about.VersionText;
        AboutBuildDateText = about.BuildDateText;
        AboutLicenseExpiryText = about.LicenseExpiryText;
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
        => PostToUi(() =>
        {
            ApplySettingsSnapshot(services.QuerySettings(), true);
            ApplyAboutSnapshot();
        });

    private void OnWorkspacesChanged(object? sender, EventArgs e)
        => PostToUi(ApplyWorkspacesSnapshot);

    private void OnModulesChanged(object? sender, EventArgs e)
        => PostToUi(ApplyModuleSettingsCardsSnapshot);

    private void OnWorkspacesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => NotifyPropertyChanged(nameof(ShowWorkspacesEmptyState));

    private void OnModuleSettingsCardsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => NotifyPropertyChanged(nameof(ShowModulesEmptyState));

    private void PostToUi(Action action)
        => _synchronizationContext.Post(_ => action(), null);

    private void SetSettingsField<T>(ref T field, T value)
    {
        if (!SetField(ref field, value))
        {
            return;
        }

        if (_isApplyingSnapshot)
        {
            return;
        }

        IsDirty = true;
    }
}
