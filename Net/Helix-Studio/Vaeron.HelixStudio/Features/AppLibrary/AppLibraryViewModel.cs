using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vaeron.HelixStudio.Features.AppLibrary.Contracts;
using Vaeron.HelixStudio.Features.WorkspaceManagement;
using Vaeron.Platform;
using Vaeron.Platform.AppRuntime;

namespace Vaeron.HelixStudio.Features.AppLibrary;

public sealed partial class AppLibraryViewModel(
    AppLibraryDomain domain,
    WorkspaceManagementFacade workspaceFacade,
    ConfirmationRuntime confirmationRuntime,
    NotificationRuntime notificationRuntime) : ViewModelBase
{
    private IReadOnlyList<LibraryAppSnapshot> _all = [];
    private int? _activeWorkspaceId;

    public ObservableCollection<LibraryAppSnapshot> Apps { get; } = [];
    public ObservableCollection<LibraryCategorySnapshot> Categories { get; } = [];

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _selectedCategoryId = "all";

    [ObservableProperty]
    private string _importFeedback = "";

    [ObservableProperty]
    private LibraryAppSnapshot? _expandedApp;

    #region 生命週期

    public void ApplyViewLoaded()
    {
        domain.Changed += OnChanged;
        workspaceFacade.WorkspaceChanged += OnWorkspaceChanged;

        Categories.Clear();
        Categories.Add(new LibraryCategorySnapshot { Id = "all", Label = "全部" });
        foreach (var c in domain.LoadCategoryList())
            Categories.Add(c);

        RunSafe(ApplySnapshotAsync);
    }

    public void ApplyViewUnloaded()
    {
        domain.Changed -= OnChanged;
        workspaceFacade.WorkspaceChanged -= OnWorkspaceChanged;
    }

    private void OnChanged() => RunSafe(ApplySnapshotAsync);
    private void OnWorkspaceChanged() => RunSafe(ApplySnapshotAsync);

    private async Task ApplySnapshotAsync()
    {
        var workspace = await workspaceFacade.LoadActiveWorkspaceAsync();
        _activeWorkspaceId = workspace?.Id;
        var items = await domain.LoadLibraryAsync(_activeWorkspaceId);
        PostToUi(() =>
        {
            _all = items;
            ApplyFilter();
        });
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedCategoryIdChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        IEnumerable<LibraryAppSnapshot> query = _all;
        if (SelectedCategoryId != "all")
            query = query.Where(a => a.CategoryId == SelectedCategoryId);
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(a =>
                a.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                a.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                a.Developer.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        Apps.Clear();
        foreach (var a in query)
            Apps.Add(a);
    }

    #endregion

    #region 使用者操作入口

    [RelayCommand]
    private async Task ApplyImportAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "可安裝工具 (*.exe;*.msi;*.zip)|*.exe;*.msi;*.zip",
            Title = "選擇要匯入的工具",
        };
        if (dialog.ShowDialog() != true) return;

        await ApplyImportFileAsync(dialog.FileName);
    }

    /// <summary>拖放匯入與對話框匯入共用同一段邏輯——副檔名／大小驗證都在 Domain 端做
    /// （見 ImportToolAsync），這裡只是換一種取得檔案路徑的方式，不重寫驗證規則。</summary>
    public async Task ApplyImportFileAsync(string filePath)
    {
        var (success, message) = await domain.ImportToolAsync(new ImportToolRequest { FilePath = filePath, WorkspaceId = _activeWorkspaceId });
        ImportFeedback = message;
        notificationRuntime.ApplyShowResult(success, message);
    }

    [RelayCommand]
    private async Task ApplyAddToWorkspaceAsync(LibraryAppSnapshot app)
    {
        if (_activeWorkspaceId is null) return;
        var (success, message) = await domain.AddToWorkspaceAsync(app.CatalogId, _activeWorkspaceId.Value);
        notificationRuntime.ApplyShowResult(success, message);
    }

    [RelayCommand]
    private async Task ApplyRemoveFromWorkspaceAsync(LibraryAppSnapshot app)
    {
        if (_activeWorkspaceId is null) return;
        var confirmed = await confirmationRuntime.AskAsync($"確定要從目前工作區移除「{app.Name}」嗎？此動作無法復原。");
        if (!confirmed) return;

        var (success, message) = await domain.RemoveFromWorkspaceAsync(app.Name, _activeWorkspaceId.Value);
        notificationRuntime.ApplyShowResult(success, message);
    }

    [RelayCommand]
    private void ApplyToggleExpand(LibraryAppSnapshot app)
        => ExpandedApp = ExpandedApp is not null && ExpandedApp.CatalogId == app.CatalogId ? null : app;

    [RelayCommand]
    private async Task ApplyRemoveImportedAsync(LibraryAppSnapshot app)
    {
        var confirmed = await confirmationRuntime.AskAsync($"確定要從工具庫移除「{app.Name}」嗎？此動作無法復原。");
        if (!confirmed) return;

        // CatalogId 對映 imported_catalog_entry.id 需要在 Domain 內部查表，這裡直接呼叫時用名稱比對
        // 交由 Domain 之後擴充；目前先用內部查詢輔助方法透過名稱移除。
        var (success, message) = await domain.RemoveImportedByCatalogIdAsync(app.CatalogId);
        notificationRuntime.ApplyShowResult(success, message);
    }

    #endregion
}
