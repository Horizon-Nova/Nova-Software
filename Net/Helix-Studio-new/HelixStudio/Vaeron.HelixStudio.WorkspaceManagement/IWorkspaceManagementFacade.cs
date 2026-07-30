using Vaeron.Kernel;

namespace Vaeron.HelixStudio.WorkspaceManagement;

/// <summary>供其他 Feature（Shell／AppsManagement／EventLog／Settings...）取得工作區資料、
/// 下工作區操作命令的唯一入口，不外洩 Store／Service 內部型別。</summary>
public interface IWorkspaceManagementFacade
{
    public event Action? WorkspaceChanged;

    public Task<IReadOnlyList<WorkspaceSnapshot>> LoadWorkspaceListAsync();

    public Task<WorkspaceSnapshot?> LoadActiveWorkspaceAsync();

    public Task<Result> SwitchActiveWorkspaceAsync(int id);

    public Task<Result> CreateWorkspaceAsync(CreateWorkspaceRequest request);

    public Task<Result> RenameWorkspaceAsync(RenameWorkspaceRequest request);

    public Task<Result> DeleteWorkspaceAsync(int id);
}
