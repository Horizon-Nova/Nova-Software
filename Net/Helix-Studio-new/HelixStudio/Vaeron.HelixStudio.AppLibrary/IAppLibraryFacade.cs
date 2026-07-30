using Vaeron.Kernel;

namespace Vaeron.HelixStudio.AppLibrary;

/// <summary>本 Feature 無其他 Feature 呼叫方，此介面存在的理由是可見性邊界——
/// ViewModel 是公開型別，建構子參數不能小於它的存取範圍（CS0051），故仍需要一層公開入口
/// 把 internal 的 Service／Store 包起來，不是為了跨 Feature 共用。</summary>
public interface IAppLibraryFacade
{
    public event Action? Changed;

    public IReadOnlyList<LibraryCategorySnapshot> LoadCategoryList();

    public Task<IReadOnlyList<LibraryAppSnapshot>> LoadLibraryAsync(int? activeWorkspaceId);

    public Task<Result> ImportToolAsync(ImportToolRequest request);

    public Task<Result> AddToWorkspaceAsync(AddToWorkspaceRequest request);

    public Task<Result> RemoveFromWorkspaceAsync(RemoveFromWorkspaceRequest request);

    public Task<Result> RemoveImportedByCatalogIdAsync(string catalogId);
}
