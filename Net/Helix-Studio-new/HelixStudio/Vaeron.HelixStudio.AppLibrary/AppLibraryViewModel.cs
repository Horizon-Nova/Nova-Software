using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vaeron.HelixStudio.Internal.Runtime;
using Vaeron.HelixStudio.Internal.Runtime.AppRuntime;
using Vaeron.HelixStudio.Internal.Runtime.Components;
using Vaeron.HelixStudio.WorkspaceManagement;

namespace Vaeron.HelixStudio.AppLibrary;

public sealed partial class AppLibraryViewModel(
    IAppLibraryFacade appLibrary,
    IWorkspaceManagementFacade workspaceFacade,
    ConfirmationRuntime confirmationRuntime,
    NotificationRuntime notificationRuntime) : ViewModelBase
{
    private IReadOnlyList<LibraryAppSnapshot> _all = [];
    private int? _activeWorkspaceId;

    public ObservableCollection<ExpandableRow<LibraryAppSnapshot>> Apps { get; } = [];

    /// <summary>分類清單：固定常數，不隨畫面重進而變動，故不放進 ApplyViewLoaded</summary>
    public ObservableCollection<LibraryCategorySnapshot> Categories { get; } =
        [new LibraryCategorySnapshot { Id = "all", Label = "全部" }, .. appLibrary.LoadCategoryList()];

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _selectedCategoryId = "all";

    [ObservableProperty]
    private string _importFeedback = "";

    [ObservableProperty]
    private string? _expandedAppCatalogId;

    [ObservableProperty]
    private bool _appsLoading = true;

    #region 生命週期

    public void ApplyViewLoaded()
    {
        appLibrary.Changed += OnChanged;
        workspaceFacade.WorkspaceChanged += OnWorkspaceChanged;

        RunSafe(ApplySnapshotAsync);
    }

    public void ApplyViewUnloaded()
    {
        appLibrary.Changed -= OnChanged;
        workspaceFacade.WorkspaceChanged -= OnWorkspaceChanged;
    }

    private void OnChanged() => RunSafe(ApplySnapshotAsync);
    private void OnWorkspaceChanged() => RunSafe(ApplySnapshotAsync);

    private async Task ApplySnapshotAsync()
    {
        var workspace = await workspaceFacade.LoadActiveWorkspaceAsync();
        _activeWorkspaceId = workspace?.Id;
        var items = await appLibrary.LoadLibraryAsync(_activeWorkspaceId);
        PostToUi(() =>
        {
            _all = items;
            ApplyFilter();
            AppsLoading = false;
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
        foreach (var row in ExpandableRows.Build(query, ExpandedAppCatalogId, a => a.CatalogId))
            Apps.Add(row);
    }

    #endregion

    #region 使用者操作入口

    [RelayCommand]
    private async Task ApplyImportAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "可安裝工具 (*.exe;*.zip)|*.exe;*.zip",
            Title = "選擇要匯入的工具",
        };
        if (dialog.ShowDialog() != true) return;

        await ApplyImportFileAsync(dialog.FileName);
    }

    /// <summary>匯入指定路徑的檔案（拖放／對話框共用）</summary>
    public async Task ApplyImportFileAsync(string filePath)
    {
        var result = await appLibrary.ImportToolAsync(new ImportToolRequest { FilePath = filePath, WorkspaceId = _activeWorkspaceId });
        ImportFeedback = result.Message;
        if (result.IsSuccess) notificationRuntime.ApplyShowSuccess(result.Message);
        else notificationRuntime.ApplyShowFailure(result.Message);
    }

    [RelayCommand]
    private async Task ApplyAddToWorkspaceAsync(LibraryAppSnapshot app)
    {
        if (_activeWorkspaceId is null) return;
        var result = await appLibrary.AddToWorkspaceAsync(new AddToWorkspaceRequest(app.CatalogId, _activeWorkspaceId.Value));
        if (result.IsSuccess) notificationRuntime.ApplyShowSuccess(result.Message);
        else notificationRuntime.ApplyShowFailure(result.Message);
    }

    [RelayCommand]
    private async Task ApplyRemoveFromWorkspaceAsync(LibraryAppSnapshot app)
    {
        if (_activeWorkspaceId is null) return;
        var confirmed = await confirmationRuntime.AskAsync($"確定要從目前工作區移除「{app.Name}」嗎？此動作無法復原。");
        if (!confirmed) return;

        var result = await appLibrary.RemoveFromWorkspaceAsync(new RemoveFromWorkspaceRequest(app.Name, _activeWorkspaceId.Value));
        if (result.IsSuccess) notificationRuntime.ApplyShowSuccess(result.Message);
        else notificationRuntime.ApplyShowFailure(result.Message);
    }

    [RelayCommand]
    private void ApplyToggleExpand(LibraryAppSnapshot app)
    {
        ExpandedAppCatalogId = ExpandableRows.Toggle(ExpandedAppCatalogId, app.CatalogId);
        ApplyFilter();
    }

    [RelayCommand]
    private async Task ApplyRemoveImportedAsync(LibraryAppSnapshot app)
    {
        var confirmed = await confirmationRuntime.AskAsync($"確定要從工具庫移除「{app.Name}」嗎？此動作無法復原。");
        if (!confirmed) return;

        var result = await appLibrary.RemoveImportedByCatalogIdAsync(app.CatalogId);
        if (result.IsSuccess) notificationRuntime.ApplyShowSuccess(result.Message);
        else notificationRuntime.ApplyShowFailure(result.Message);
    }

    #endregion
}
