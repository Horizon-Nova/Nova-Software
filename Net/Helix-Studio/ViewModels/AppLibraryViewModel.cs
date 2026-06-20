using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using Helix_Studio.Data;
using Helix_Studio.Models;
using Helix_Studio.Mvvm;
using Helix_Studio.Services.Tools;

namespace Helix_Studio.ViewModels;

/// <summary>
/// 應用程式庫頁：瀏覽目錄工具、詳細介紹、安裝/解除安裝、匯入。
/// </summary>
public sealed class AppLibraryViewModel : ViewModelBase
{
    private readonly ToolCatalogService _catalog;
    private readonly ToolInstallService _install;
    private readonly InstalledToolsRepository _repository;
    private string _workspaceId;

    private string _searchText = string.Empty;
    private CatalogTool? _selectedTool;
    private string _importStatus = string.Empty;

    public override string PageKey => "library";

    public ObservableCollection<CatalogTool> Tools { get; } = new();
    public ICollectionView ToolsView { get; }

    public ICommand InstallCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand ImportCommand { get; }

    /// <summary>安裝/解除安裝後通知 Shell 重新整理應用程式頁。</summary>
    public event Action? InstalledChanged;

    public AppLibraryViewModel(ToolCatalogService catalog, ToolInstallService install,
        InstalledToolsRepository repository, string workspaceId)
    {
        _catalog = catalog;
        _install = install;
        _repository = repository;
        _workspaceId = workspaceId;

        ToolsView = CollectionViewSource.GetDefaultView(Tools);
        ToolsView.Filter = FilterTool;

        InstallCommand = new RelayCommand(p => Install(p as CatalogTool ?? _selectedTool));
        UninstallCommand = new RelayCommand(p => Uninstall(p as CatalogTool ?? _selectedTool));
        ImportCommand = new RelayCommand(Import);

        Reload();
    }

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) ToolsView.Refresh(); }
    }

    public CatalogTool? SelectedTool
    {
        get => _selectedTool;
        set
        {
            if (SetProperty(ref _selectedTool, value))
                OnPropertyChanged(nameof(HasSelection));
        }
    }

    public bool HasSelection => _selectedTool is not null;

    /// <summary>最近一次匯入的結果訊息（成功或失敗），供畫面顯示。</summary>
    public string ImportStatus
    {
        get => _importStatus;
        set => SetProperty(ref _importStatus, value);
    }

    /// <summary>切換工作區：重新指向新工作區並重整安裝狀態。</summary>
    public void SwitchWorkspace(string workspaceId)
    {
        _workspaceId = workspaceId;
        Reload();
    }

    public void Reload()
    {
        var installedIds = _repository.Load(_workspaceId).Select(r => r.ToolId).ToHashSet();
        var selectedId = _selectedTool?.Id;

        Tools.Clear();
        foreach (var manifest in _catalog.LoadCatalog().OrderBy(m => m.Category).ThenBy(m => m.Name))
            Tools.Add(new CatalogTool { Manifest = manifest, IsInstalled = installedIds.Contains(manifest.Id) });

        SelectedTool = selectedId is not null
            ? Tools.FirstOrDefault(t => t.Id == selectedId)
            : Tools.FirstOrDefault();
    }

    private bool FilterTool(object obj)
    {
        if (obj is not CatalogTool tool) return false;
        if (string.IsNullOrWhiteSpace(_searchText)) return true;
        return tool.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || tool.Manifest.Description.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || tool.Manifest.Category.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    private void Install(CatalogTool? tool)
    {
        if (tool is null || tool.IsInstalled) return;
        _install.Install(_workspaceId, tool.Manifest);
        tool.IsInstalled = true;
        InstalledChanged?.Invoke();
    }

    private void Uninstall(CatalogTool? tool)
    {
        if (tool is null || !tool.IsInstalled) return;
        _install.Uninstall(_workspaceId, tool.Id);
        tool.IsInstalled = false;
        InstalledChanged?.Invoke();
    }

    private void Import()
    {
        var dialog = new OpenFolderDialog { Title = "選擇工具資料夾（須含 tool.json）" };
        if (dialog.ShowDialog() != true) return;

        var manifest = _install.ImportFromFolder(dialog.FolderName);
        if (manifest is null)
        {
            ImportStatus = "匯入失敗：所選資料夾須包含有效的 tool.json。";
            return;
        }

        // 若該工具已安裝於目前工作區 → 覆蓋安裝（升級到匯入的版本）
        var alreadyInstalled = _repository.Load(_workspaceId).Any(r => r.ToolId == manifest.Id);
        if (alreadyInstalled) _install.Install(_workspaceId, manifest);

        Reload();
        SelectedTool = Tools.FirstOrDefault(t => t.Id == manifest.Id);
        ImportStatus = alreadyInstalled
            ? $"已匯入並更新：{manifest.Name}（v{manifest.Version}）"
            : $"已匯入：{manifest.Name}（v{manifest.Version}）";

        if (alreadyInstalled) InstalledChanged?.Invoke();
    }
}
