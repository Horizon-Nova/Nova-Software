using System;
using Helix_Studio.Model.Modules;
using Helix_Studio.Services;
using Helix_Studio.ViewModels.Base;

namespace Helix_Studio.ViewModels;

/// <summary>模組分頁：已載入清單、選取、重新掃描、匯入與停用／解除安裝。</summary>
public sealed class ModulesPageViewModel : ViewModelBase, IDisposable
{
    private readonly ModulesCatalogServices _catalog;
    private readonly ModuleRuntimeServices _runtime;

    private IReadOnlyList<ModuleListItem> _items = [];
    private ModuleListItem? _selected;

    /// <summary>要求內容區開啟指定模組識別之內容。</summary>
    public event Action<string>? OpenModuleRequested;

    /// <summary>要求顯示從資料夾匯入模組的 UI（例如對話框）。</summary>
    public event Action? ImportRequested;

    /// <summary>要求停用目前選取之模組（可與專屬按鈕併用）。</summary>
    public event Action? DisableRequested;

    /// <summary>要求解除安裝目前選取之模組（可與專屬按鈕併用）。</summary>
    public event Action? UninstallRequested;

    /// <summary>使用 <see cref="AppServices"/> 模組相關服務的預設建構子。</summary>
    public ModulesPageViewModel()
        : this(AppServices.ModulesCatalog, AppServices.ModuleRuntime) { }

    /// <summary>以模組目錄與執行時期服務建立，並訂閱掃描完成事件以重新載入清單。</summary>
    public ModulesPageViewModel(ModulesCatalogServices catalog, ModuleRuntimeServices runtime)
    {
        _catalog = catalog;
        _runtime = runtime;

        _runtime.CatalogChanged += OnCatalogChanged;

        RescanCommand = new RelayCommand(DoRescan);
        ImportCommand = new RelayCommand(() => ImportRequested?.Invoke());
        OpenCommand = new RelayCommand(DoOpen, () => _selected is not null);
        DisableCommand = new RelayCommand(() => DisableRequested?.Invoke(), () => _selected is not null);
        UninstallCommand = new RelayCommand(() => UninstallRequested?.Invoke(), () => _selected is not null);

        ImportFromFolderCommand = new RelayCommand(ImportFromFolderByParameter);
        DisableSelectedCommand = new RelayCommand(DisableSelected, () => _selected is not null);
        UninstallSelectedCommand = new RelayCommand(UninstallSelected, () => _selected is not null);

        LoadSnapshot();
    }

    /// <summary>觸發背景重新掃描模組目錄。</summary>
    public RelayCommand RescanCommand { get; }

    /// <summary>顯示匯入模組的入口（觸發 <see cref="ImportRequested"/>）。</summary>
    public RelayCommand ImportCommand { get; }

    /// <summary>開啟目前選取模組（觸發 <see cref="OpenModuleRequested"/>）。</summary>
    public RelayCommand OpenCommand { get; }

    /// <summary>停用目前選取模組（觸發 <see cref="DisableRequested"/>）。</summary>
    public RelayCommand DisableCommand { get; }

    /// <summary>解除安裝目前選取模組（觸發 <see cref="UninstallRequested"/>）。</summary>
    public RelayCommand UninstallCommand { get; }

    /// <summary>參數為來源資料夾路徑，複製至安裝目錄後重新掃描。</summary>
    public RelayCommand ImportFromFolderCommand { get; }

    /// <summary>卸載目前選取模組但不刪除檔案。</summary>
    public RelayCommand DisableSelectedCommand { get; }

    /// <summary>卸載並刪除目前選取模組之安裝目錄。</summary>
    public RelayCommand UninstallSelectedCommand { get; }

    /// <summary>已載入模組的唯讀清單。</summary>
    public IReadOnlyList<ModuleListItem> Items => _items;

    /// <summary>清單中目前選取項目；變更時重算命令可用性。</summary>
    public ModuleListItem? SelectedItem
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(HasSelection));
            RaisePropertyChanged(nameof(NoSelection));
            RelayCommand.InvalidateAll();
        }
    }

    /// <summary>是否已選取一筆模組。</summary>
    public bool HasSelection => _selected is not null;

    /// <summary>是否未選取任何模組（空選取狀態）。</summary>
    public bool NoSelection => _selected is null;

    /// <summary>應顯示「尚無模組」空狀態版面。</summary>
    public bool ShowEmptyState => _items.Count == 0;

    /// <summary>應顯示模組清單（至少一筆）。</summary>
    public bool ShowList => _items.Count > 0;

    /// <summary>目前工作區或預設的模組實體安裝目錄路徑。</summary>
    public string ModulesDirectoryPath => _catalog.QueryInstallDirectoryPath();

    private void LoadSnapshot()
    {
        var previousId = _selected?.Id;
        _items = _catalog.QueryLoadedModules();

        RaisePropertyChanged(nameof(Items));
        RaisePropertyChanged(nameof(ShowEmptyState));
        RaisePropertyChanged(nameof(ShowList));

        SelectedItem = previousId is null
            ? null
            : _items.FirstOrDefault(m =>
                string.Equals(m.Id, previousId, StringComparison.OrdinalIgnoreCase));

        RelayCommand.InvalidateAll();
    }

    private void OnCatalogChanged(object? sender, EventArgs e)
    {
        LoadSnapshot();
    }

    private void DoRescan()
    {
        _catalog.RequestScan();
    }

    private void ImportFromFolderByParameter(object? parameter)
    {
        var folder = parameter as string;
        if (string.IsNullOrWhiteSpace(folder)) return;
        _catalog.ImportFromFolder(folder);
        LoadSnapshot();
    }

    private void DoOpen()
    {
        if (_selected is null) return;
        OpenModuleRequested?.Invoke(_selected.Id);
    }

    private void DisableSelected()
    {
        if (_selected is null) return;
        _catalog.Unload(_selected.Id, deleteFolder: false);
        LoadSnapshot();
    }

    private void UninstallSelected()
    {
        if (_selected is null) return;
        _catalog.Unload(_selected.Id);
        LoadSnapshot();
    }

    /// <summary>取消訂閱模組掃描完成事件。</summary>
    public void Dispose()
    {
        _runtime.CatalogChanged -= OnCatalogChanged;
    }
}
