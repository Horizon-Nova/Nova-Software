using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Input;
using Helix_Studio.AppHost;
using Helix_Studio.Domain;
using Helix_Studio.Model;
using Helix_Studio.ViewModels.Base;

namespace Helix_Studio.ViewModels;

/// <summary>
/// 設定頁面的 ViewModel（UI 狀態機）。
/// </summary>
public sealed class SettingsPageViewModel(
    ApplicationRuntime applicationRuntime,
    SettingsDomain domain,
    NotificationRuntime notificationRuntime,
    LocalizationRuntime localizationRuntime) : ViewModelBase, IDisposable
{
    private const string ZhHantLanguageCode = "zh-Hant";
    private const string EnLanguageCode = "en";

    private static readonly (string Tag, string DisplayKey)[] SectionSuggestions =
    [
        ("General", "Loc_Settings_SectionGeneral"),
        ("Display", "Loc_Settings_SectionDisplay"),
        ("Workspaces", "Loc_Settings_SectionWorkspaces"),
        ("Modules", "Loc_Settings_SectionModules"),
        ("Log", "Loc_Settings_SectionLog"),
        ("About", "Loc_Settings_SectionAbout"),
    ];

    private readonly ObservableCollection<SettingsSearchSuggestionModel> _searchSuggestions = [];
    private readonly ObservableCollection<SettingsWorkspaceItemModel> _workspaces = [];
    private readonly ObservableCollection<SettingsModuleCardModel> _moduleSettingsCards = [];

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
    private bool _isSaving;
    private int _baselineLanguageIndex;
    private int _baselineStartPageIndex;
    private int _baselineInactivityIndex;
    private int _baselineThemeIndex;
    private int _baselineFontScaleIndex;
    private int _baselineDataDensityIndex;
    private int _baselineLogLevelIndex;
    private int _baselineLogRetentionIndex;
    private string _baselineLogExportFolder = string.Empty;

    private string _searchQuery = string.Empty;
    private bool _isSearchPopupOpen;
    private SettingsSearchSuggestionModel? _selectedSearchSuggestion;

    private bool _isDirty;
    private int _dataDensityIndex;
    private int _fontScaleIndex;
    private int _inactivityIndex;
    private int _languageIndex;
    private string _logExportFolder = string.Empty;
    private int _logLevelIndex;
    private int _logRetentionIndex;
    private int _startPageIndex;
    private int _themeIndex;
    private string _aboutVersionText = "-";
    private string _aboutBuildDateText = "-";
    private string _aboutLicenseExpiryText = "-";

    /// <summary>
    /// 搜尋輸入內容。
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
    /// 是否顯示搜尋建議彈出層。
    /// </summary>
    public bool IsSearchPopupOpen
    {
        get => _isSearchPopupOpen;
        set => SetField(ref _isSearchPopupOpen, value);
    }

    /// <summary>
    /// 搜尋建議清單（供 XAML 綁定）。
    /// </summary>
    public ObservableCollection<SettingsSearchSuggestionModel> SearchSuggestions => _searchSuggestions;

    /// <summary>
    /// 當前選取的搜尋建議。
    /// </summary>
    public SettingsSearchSuggestionModel? SelectedSearchSuggestion
    {
        get => _selectedSearchSuggestion;
        set => SetField(ref _selectedSearchSuggestion, value);
    }

    /// <summary>
    /// 是否存在尚未儲存的變更。
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
    /// 工作區清單（供 XAML 綁定）。
    /// </summary>
    public ObservableCollection<SettingsWorkspaceItemModel> Workspaces => _workspaces;

    /// <summary>
    /// 是否顯示工作區空狀態。
    /// </summary>
    public bool ShowWorkspacesEmptyState => Workspaces.Count == 0;

    /// <summary>
    /// 模組卡片資料來源（供 XAML 綁定）。
    /// </summary>
    public ObservableCollection<SettingsModuleCardModel> ModuleSettingsCards => _moduleSettingsCards;

    /// <summary>
    /// 是否顯示模組空狀態。
    /// </summary>
    public bool ShowModulesEmptyState => ModuleSettingsCards.Count == 0;

    public int LanguageIndex
    {
        get => _languageIndex;
        set => SetSettingsField(ref _languageIndex, value);
    }

    public int StartPageIndex
    {
        get => _startPageIndex;
        set => SetSettingsField(ref _startPageIndex, value);
    }

    public int InactivityIndex
    {
        get => _inactivityIndex;
        set => SetSettingsField(ref _inactivityIndex, value);
    }

    public int ThemeIndex
    {
        get => _themeIndex;
        set => SetSettingsField(ref _themeIndex, value);
    }

    public int FontScaleIndex
    {
        get => _fontScaleIndex;
        set => SetSettingsField(ref _fontScaleIndex, value);
    }

    public int DataDensityIndex
    {
        get => _dataDensityIndex;
        set => SetSettingsField(ref _dataDensityIndex, value);
    }

    public int LogLevelIndex
    {
        get => _logLevelIndex;
        set => SetSettingsField(ref _logLevelIndex, value);
    }

    public int LogRetentionIndex
    {
        get => _logRetentionIndex;
        set => SetSettingsField(ref _logRetentionIndex, value);
    }

    public string LogExportFolder
    {
        get => _logExportFolder;
        set => SetSettingsField(ref _logExportFolder, value);
    }

    public string AboutVersionText
    {
        get => _aboutVersionText;
        private set => SetField(ref _aboutVersionText, value);
    }

    public string AboutBuildDateText
    {
        get => _aboutBuildDateText;
        private set => SetField(ref _aboutBuildDateText, value);
    }

    public string AboutLicenseExpiryText
    {
        get => _aboutLicenseExpiryText;
        private set => SetField(ref _aboutLicenseExpiryText, value);
    }

    public ICommand ReloadCommand
        => _reloadCommand ??= new RelayCommand(_ =>
        {
            ApplyAllSnapshots(markDirty: false);
            notificationRuntime.Request(new NotificationRequest
            {
                Title = localizationRuntime.QueryString("Loc_Toast_SettingsReloadedTitle"),
                Message = localizationRuntime.QueryString("Loc_Toast_SettingsReloadedBody"),
                Severity = NotificationSeverity.Information,
            });
        });

    public ICommand SaveCommand
        => _saveCommand ??= new RelayCommand(_ => ApplySave(), _ => IsDirty);

    public ICommand DiscardCommand
        => _discardCommand ??= new RelayCommand(_ =>
        {
            ApplyDiscard();
            notificationRuntime.Request(new NotificationRequest
            {
                Title = localizationRuntime.QueryString("Loc_Toast_SettingsDiscardedTitle"),
                Message = localizationRuntime.QueryString("Loc_Toast_SettingsDiscardedBody"),
                Severity = NotificationSeverity.Information,
            });
        }, _ => IsDirty);

    public ICommand ActivateWorkspaceCommand
        => _activateWorkspaceCommand ??= new RelayCommand(parameter => ApplyActivateWorkspace(parameter?.ToString() ?? string.Empty));

    public ICommand DeleteWorkspaceCommand
        => _deleteWorkspaceCommand ??= new RelayCommand(parameter => ApplyDeleteWorkspace(parameter?.ToString() ?? string.Empty));

    public ICommand ExportLogsCommand
        => _exportLogsCommand ??= new RelayCommand(parameter => ApplyExportLogs(parameter?.ToString() ?? string.Empty));

    public ICommand OpenLegalNoticesCommand
        => _openLegalNoticesCommand ??= new RelayCommand(_ => domain.OpenLegalNotices(ResolveLanguageCode(LanguageIndex)));

    /// <summary>
    /// 由 View 呼叫：標記 ViewModel 已載入並掛上事件。
    /// </summary>
    internal void ApplyViewLoaded()
    {
        if (_isInitialized)
        {
            return;
        }

        domain.SettingsChanged += OnChanged;
        domain.WorkspacesChanged += OnChanged;
        Workspaces.CollectionChanged += OnWorkspacesChanged;

        ApplyAllSnapshots(markDirty: false);
        _isInitialized = true;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        if (_isInitialized)
        {
            domain.SettingsChanged -= OnChanged;
            domain.WorkspacesChanged -= OnChanged;
            Workspaces.CollectionChanged -= OnWorkspacesChanged;
        }

        _isDisposed = true;
    }

    private void OnChanged(object? sender, EventArgs e)
        => applicationRuntime.InvokeOnUiThread(() =>
        {
            if (IsDirty && !_isSaving)
            {
                ApplyNonEditSnapshots();
                return;
            }

            ApplyAllSnapshots(markDirty: false);
        });

    private void OnWorkspacesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => NotifyPropertyChanged(nameof(ShowWorkspacesEmptyState));

    private void ApplyAllSnapshots(bool markDirty)
    {
        ApplySettingsSnapshot(domain.QuerySettings(), markDirty);
        ApplyNonEditSnapshots();
    }

    private void ApplyDiscard()
    {
        ApplyBaselineFields();
        ApplyNonEditSnapshots();
    }

    private void ApplyNonEditSnapshots()
    {
        ApplyWorkspacesSnapshot();
        ApplyAboutSnapshot();
        ApplySearchSuggestions();
    }

    private void ApplySettingsSnapshot(SettingsModel settings, bool markDirty)
    {
        if (!markDirty)
        {
            _baselineLanguageIndex = settings.LanguageIndex;
            _baselineStartPageIndex = settings.StartPageIndex;
            _baselineInactivityIndex = settings.InactivityIndex;
            _baselineThemeIndex = settings.ThemeIndex;
            _baselineFontScaleIndex = settings.FontScaleIndex;
            _baselineDataDensityIndex = settings.DataDensityIndex;
            _baselineLogLevelIndex = settings.LogLevelIndex;
            _baselineLogRetentionIndex = settings.LogRetentionIndex;
            _baselineLogExportFolder = settings.LogExportFolder ?? string.Empty;
        }

        _isApplyingSnapshot = true;

        LanguageIndex = settings.LanguageIndex;
        StartPageIndex = settings.StartPageIndex;
        InactivityIndex = settings.InactivityIndex;
        ThemeIndex = settings.ThemeIndex;
        FontScaleIndex = settings.FontScaleIndex;
        DataDensityIndex = settings.DataDensityIndex;
        LogLevelIndex = settings.LogLevelIndex;
        LogRetentionIndex = settings.LogRetentionIndex;
        LogExportFolder = settings.LogExportFolder ?? string.Empty;

        _isApplyingSnapshot = false;

        if (!markDirty)
        {
            IsDirty = false;
        }
    }

    private void ApplyBaselineFields()
    {
        var previousLanguageIndex = LanguageIndex;
        var previousStartPageIndex = StartPageIndex;
        var previousInactivityIndex = InactivityIndex;
        var previousThemeIndex = ThemeIndex;
        var previousFontScaleIndex = FontScaleIndex;
        var previousDataDensityIndex = DataDensityIndex;
        var previousLogLevelIndex = LogLevelIndex;
        var previousLogRetentionIndex = LogRetentionIndex;
        var previousLogExportFolder = LogExportFolder;

        _isApplyingSnapshot = true;

        LanguageIndex = _baselineLanguageIndex;
        StartPageIndex = _baselineStartPageIndex;
        InactivityIndex = _baselineInactivityIndex;
        ThemeIndex = _baselineThemeIndex;
        FontScaleIndex = _baselineFontScaleIndex;
        DataDensityIndex = _baselineDataDensityIndex;
        LogLevelIndex = _baselineLogLevelIndex;
        LogRetentionIndex = _baselineLogRetentionIndex;
        LogExportFolder = _baselineLogExportFolder;

        _isApplyingSnapshot = false;

        NotifyPropertyChanged(nameof(LanguageIndex));
        NotifyPropertyChanged(nameof(StartPageIndex));
        NotifyPropertyChanged(nameof(InactivityIndex));
        NotifyPropertyChanged(nameof(ThemeIndex));
        NotifyPropertyChanged(nameof(FontScaleIndex));
        NotifyPropertyChanged(nameof(DataDensityIndex));
        NotifyPropertyChanged(nameof(LogLevelIndex));
        NotifyPropertyChanged(nameof(LogRetentionIndex));
        NotifyPropertyChanged(nameof(LogExportFolder));

        if (previousLanguageIndex == LanguageIndex
            && previousStartPageIndex == StartPageIndex
            && previousInactivityIndex == InactivityIndex
            && previousThemeIndex == ThemeIndex
            && previousFontScaleIndex == FontScaleIndex
            && previousDataDensityIndex == DataDensityIndex
            && previousLogLevelIndex == LogLevelIndex
            && previousLogRetentionIndex == LogRetentionIndex
            && string.Equals(previousLogExportFolder, LogExportFolder, StringComparison.Ordinal))
        {
            // 綁定未更新時，強制觸發一次全欄位通知，確保 UI 與 Edit Buffer 同步。
            NotifyPropertyChanged(string.Empty);
        }

        IsDirty = false;
    }

    private void ApplyWorkspacesSnapshot()
    {
        var workspaces = domain.QueryWorkspaces();
        Workspaces.Clear();
        foreach (var item in workspaces)
        {
            Workspaces.Add(item);
        }
    }

    private void ApplyAboutSnapshot()
    {
        var about = domain.QueryAbout();
        AboutVersionText = about.VersionText;
        AboutBuildDateText = about.BuildDateText;
        AboutLicenseExpiryText = about.LicenseExpiryText;
    }

    private void ApplySave()
    {
        var snapshot = new SettingsModel
        {
            LanguageIndex = LanguageIndex,
            StartPageIndex = StartPageIndex,
            InactivityIndex = InactivityIndex,
            ThemeIndex = ThemeIndex,
            FontScaleIndex = FontScaleIndex,
            DataDensityIndex = DataDensityIndex,
            LogLevelIndex = LogLevelIndex,
            LogRetentionIndex = LogRetentionIndex,
            LogExportFolder = LogExportFolder,
        };

        try
        {
            _isSaving = true;
            domain.SaveSettings(snapshot);
            IsDirty = false;

            notificationRuntime.Request(new NotificationRequest
            {
                Title = localizationRuntime.QueryString("Loc_Toast_SettingsSavedTitle"),
                Message = localizationRuntime.QueryString("Loc_Toast_SettingsSavedBody"),
                Severity = NotificationSeverity.Success,
            });
        }
        catch (Exception ex)
        {
            notificationRuntime.Request(new NotificationRequest
            {
                Title = localizationRuntime.QueryString("Loc_Toast_SettingsSaveFailedTitle"),
                Message = ex.Message,
                Severity = NotificationSeverity.Error,
            });
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void ApplyActivateWorkspace(string workspaceName)
    {
        var normalized = (workspaceName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        domain.ActivateWorkspace(normalized);
    }

    private void ApplyDeleteWorkspace(string workspaceName)
    {
        var normalized = (workspaceName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        domain.DeleteWorkspace(normalized);
    }

    private void ApplyExportLogs(string exportDirectoryPath)
    {
        var normalized = (exportDirectoryPath ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        try
        {
            var zipPath = domain.ExportLogs(normalized);
            notificationRuntime.Request(new NotificationRequest
            {
                Title = localizationRuntime.QueryString("Loc_Toast_LogsExportedTitle"),
                Message = localizationRuntime.QueryString("Loc_Toast_LogsExportedBody"),
                Severity = NotificationSeverity.Success,
                Action = NotificationAction.RevealInFolder,
                ActionPath = zipPath,
            });
        }
        catch (Exception ex)
        {
            notificationRuntime.Request(new NotificationRequest
            {
                Title = localizationRuntime.QueryString("Loc_Toast_LogsExportFailedTitle"),
                Message = ex.Message,
                Severity = NotificationSeverity.Error,
            });
        }
    }

    private void ApplySearchSuggestions()
    {
        var keyword = (SearchQuery ?? string.Empty).Trim();

        var suggestions = SectionSuggestions
            .Select(s => new SettingsSearchSuggestionModel
            {
                SectionTag = s.Tag,
                Display = localizationRuntime.QueryString(s.DisplayKey),
            })
            .ToArray();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            suggestions = suggestions
                .Where(s => s.Display.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        SearchSuggestions.Clear();
        foreach (var suggestion in suggestions)
        {
            SearchSuggestions.Add(suggestion);
        }

        IsSearchPopupOpen = SearchSuggestions.Count > 0 && !string.IsNullOrWhiteSpace(keyword);
    }

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

    private static string ResolveLanguageCode(int languageIndex)
        => languageIndex == 0 ? ZhHantLanguageCode : EnLanguageCode;

    /// <summary>
    /// 由 View 呼叫：套用目前選取的搜尋建議（將搜尋欄位填入建議文字並關閉彈出層）。
    /// </summary>
    public void ApplySelectedSearchSuggestion()
    {
        if (SelectedSearchSuggestion is null)
        {
            return;
        }

        SearchQuery = SelectedSearchSuggestion.Display;
        IsSearchPopupOpen = false;
    }

    /// <summary>
    /// 由 View 呼叫：取得目前選取的 section tag。
    /// </summary>
    public string QuerySelectedSectionTag()
        => SelectedSearchSuggestion?.SectionTag ?? string.Empty;

    /// <summary>
    /// 由 View 呼叫：套用「匯出資料夾」輸入值（僅更新 Edit Buffer，不寫入 INI）。
    /// </summary>
    public void ApplyLogExportFolder(string folderPath)
        => LogExportFolder = folderPath ?? string.Empty;

    /// <summary>
    /// 由 View 呼叫：立即建立並啟用工作區。
    /// </summary>
    public void ApplyCreateWorkspaceNow(string workspaceName, string description)
    {
        var name = (workspaceName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            domain.CreateWorkspace(name, (description ?? string.Empty).Trim());
        }
        catch (Exception ex)
        {
            notificationRuntime.Request(new NotificationRequest
            {
                Title = localizationRuntime.QueryString("Loc_Toast_SettingsSaveFailedTitle"),
                Message = ex.Message,
                Severity = NotificationSeverity.Error,
            });
        }
    }
}
