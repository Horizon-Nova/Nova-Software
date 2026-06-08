using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using Helix_Studio.Domain;
using Helix_Studio.Model;
using Helix_Studio.ViewModels.Base;

namespace Helix_Studio.ViewModels;

/// <summary>
/// 模組頁面的 UI 狀態機。
/// </summary>
public sealed class ModulesPageViewModel(ModulesDomain domain) : ViewModelBase, IDisposable
{
    private SynchronizationContext _synchronizationContext = new();
    private RelayCommand? _rescanCommand;
    private RelayCommand? _importCommand;
    private RelayCommand? _toggleEnabledCommand;
    private RelayCommand? _uninstallCommand;
    private RelayCommand? _openCommand;
    private RelayCommand? _settingsCommand;
    private bool _isInitialized;
    private bool _isDisposed;
    private string _modulesDirectoryPath = string.Empty;
    private ModulesInstalledItemModel? _selectedItem;

    /// <summary>
    /// 模組目錄路徑。
    /// </summary>
    public string ModulesDirectoryPath
    {
        get => _modulesDirectoryPath;
        private set => SetField(ref _modulesDirectoryPath, value);
    }

    /// <summary>
    /// 已安裝模組清單。
    /// </summary>
    public ObservableCollection<ModulesInstalledItemModel> Items { get; } = [];

    /// <summary>
    /// 目前選取的模組。
    /// </summary>
    public ModulesInstalledItemModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (!SetField(ref _selectedItem, value))
            {
                return;
            }

            NotifyPropertyChanged(nameof(HasSelection));
            NotifyPropertyChanged(nameof(NoSelection));
            NotifyPropertyChanged(nameof(ToggleEnabledButtonText));
            _toggleEnabledCommand?.NotifyCanExecuteChanged();
            _uninstallCommand?.NotifyCanExecuteChanged();
            _openCommand?.NotifyCanExecuteChanged();
            _settingsCommand?.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// 是否顯示空狀態。
    /// </summary>
    public bool ShowEmptyState => Items.Count == 0;

    /// <summary>
    /// 是否顯示清單。
    /// </summary>
    public bool ShowList => Items.Count > 0;

    /// <summary>
    /// 是否尚未選取任何模組。
    /// </summary>
    public bool NoSelection => SelectedItem is null;

    /// <summary>
    /// 是否已選取模組。
    /// </summary>
    public bool HasSelection => SelectedItem is not null;

    /// <summary>
    /// 重新掃描模組目錄。
    /// </summary>
    public ICommand RescanCommand
        => _rescanCommand ??= new RelayCommand(_ => ApplySnapshot());

    /// <summary>
    /// 導入模組。
    /// </summary>
    public ICommand ImportCommand
        => _importCommand ??= new RelayCommand(_ => ImportRequested?.Invoke(this, EventArgs.Empty));

    /// <summary>
    /// 開啟模組。
    /// </summary>
    public ICommand OpenCommand
        => _openCommand ??= new RelayCommand(_ => ApplyOpen(), _ => SelectedItem is { IsLoadable: true });

    /// <summary>
    /// 開啟模組設定。
    /// </summary>
    public ICommand SettingsCommand
        => _settingsCommand ??= new RelayCommand(_ => ApplyOpenSettings(), _ => SelectedItem is { HasSettings: true, IsEnabled: true });

    /// <summary>
    /// 停用模組。
    /// </summary>
    public ICommand ToggleEnabledCommand
        => _toggleEnabledCommand ??= new RelayCommand(_ => ApplyToggleEnabled(), _ => SelectedItem is not null);

    /// <summary>
    /// 啟用或停用按鈕文案。
    /// </summary>
    public string ToggleEnabledButtonText
        => SelectedItem is { IsEnabled: false } ? "啟用模組" : "停用模組";

    /// <summary>
    /// 移除模組。
    /// </summary>
    public ICommand UninstallCommand
        => _uninstallCommand ??= new RelayCommand(_ => ApplyUninstall(), _ => SelectedItem is not null);

    /// <summary>
    /// 模組開啟請求事件（由 View 決定如何呈現模組畫面）。
    /// </summary>
    public event EventHandler<string>? ModuleOpenRequested;

    /// <summary>
    /// 模組設定開啟要求。
    /// </summary>
    public event EventHandler<string>? ModuleSettingsRequested;

    /// <summary>
    /// 導入模組請求事件（由 View 顯示資料夾挑選對話框）。
    /// </summary>
    public event EventHandler? ImportRequested;

    /// <summary>
    /// 初始化模組頁面的 I/O 訂閱與初始快照。
    /// </summary>
    internal void ApplyViewLoaded()
    {
        if (_isInitialized)
        {
            return;
        }

        _synchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext();
        domain.Changed += OnChanged;
        Items.CollectionChanged += OnItemsChanged;
        ApplySnapshot();
        _isInitialized = true;
    }

    /// <summary>
    /// 導入模組資料夾（由 View 傳入資料夾路徑）。
    /// </summary>
    /// <param name="sourceDirectoryPath">來源資料夾路徑。</param>
    internal void ImportModule(string sourceDirectoryPath)
    {
        var imported = domain.ImportModule(sourceDirectoryPath);
        ApplySnapshot(imported.Id);
    }

    /// <summary>
    /// 依模組識別值選取模組。
    /// </summary>
    /// <param name="moduleId">模組識別值。</param>
    internal void ApplySelectModule(string moduleId)
    {
        ApplySnapshot(moduleId);
    }

    /// <summary>
    /// 釋放模組頁面的事件訂閱。
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        if (_isInitialized)
        {
            domain.Changed -= OnChanged;
            Items.CollectionChanged -= OnItemsChanged;
        }

        _isDisposed = true;
    }

    private void OnChanged(object? sender, EventArgs e)
        => PostToUi(() => ApplySnapshot());

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotifyPropertyChanged(nameof(ShowEmptyState));
        NotifyPropertyChanged(nameof(ShowList));
    }

    private void PostToUi(Action action)
        => _synchronizationContext.Post(_ => action(), null);

    private void ApplySnapshot(string? preferSelectModuleId = null)
    {
        ModulesDirectoryPath = domain.QueryModulesDirectoryPath();
        var selectedModuleId = !string.IsNullOrWhiteSpace(preferSelectModuleId)
            ? preferSelectModuleId
            : SelectedItem?.Id;

        var modules = domain.QueryModules();
        Items.Clear();
        foreach (var module in modules)
        {
            Items.Add(module);
        }

        SelectedItem = string.IsNullOrWhiteSpace(selectedModuleId)
            ? null
            : Items.FirstOrDefault(item => string.Equals(item.Id, selectedModuleId, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyToggleEnabled()
    {
        if (SelectedItem is null)
        {
            return;
        }

        if (SelectedItem.IsEnabled)
        {
            domain.DisableModule(SelectedItem.Id);
            return;
        }

        domain.EnableModule(SelectedItem.Id);
    }

    private void ApplyUninstall()
    {
        if (SelectedItem is null)
        {
            return;
        }

        var installedFolderPath = SelectedItem.InstalledFolderPath;
        SelectedItem = null;
        domain.UninstallModule(installedFolderPath);
    }

    private void ApplyOpen()
    {
        if (SelectedItem is null)
        {
            return;
        }

        if (!SelectedItem.IsEnabled)
        {
            domain.EnableModule(SelectedItem.Id);
        }

        ModuleOpenRequested?.Invoke(this, SelectedItem.Id);
    }

    private void ApplyOpenSettings()
    {
        if (SelectedItem is null || !SelectedItem.HasSettings)
        {
            return;
        }

        ModuleSettingsRequested?.Invoke(this, SelectedItem.Id);
    }
}
