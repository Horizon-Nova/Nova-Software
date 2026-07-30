using Vaeron.Kernel;

namespace Vaeron.HelixStudio.AppLibrary.Internal;

internal sealed class AppLibraryFacade(AppLibraryService service) : IAppLibraryFacade
{
    public event Action? Changed
    {
        add => service.Changed += value;
        remove => service.Changed -= value;
    }

    public IReadOnlyList<LibraryCategorySnapshot> LoadCategoryList() => service.LoadCategoryList();

    public Task<IReadOnlyList<LibraryAppSnapshot>> LoadLibraryAsync(int? activeWorkspaceId)
        => service.LoadLibraryAsync(activeWorkspaceId);

    public Task<Result> ImportToolAsync(ImportToolRequest request)
        => service.ImportToolAsync(request);

    public Task<Result> AddToWorkspaceAsync(AddToWorkspaceRequest request)
        => service.AddToWorkspaceAsync(request);

    public Task<Result> RemoveFromWorkspaceAsync(RemoveFromWorkspaceRequest request)
        => service.RemoveFromWorkspaceAsync(request);

    public Task<Result> RemoveImportedByCatalogIdAsync(string catalogId)
        => service.RemoveImportedByCatalogIdAsync(catalogId);
}
