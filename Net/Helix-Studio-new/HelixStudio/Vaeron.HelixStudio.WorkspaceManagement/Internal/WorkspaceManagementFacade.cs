using Vaeron.Kernel;

namespace Vaeron.HelixStudio.WorkspaceManagement.Internal;

/// <summary>只委派,不含邏輯。Facade 停在業務層,不注入 Store。</summary>
internal sealed class WorkspaceManagementFacade(WorkspaceManagementService service) : IWorkspaceManagementFacade
{
    public event Action? WorkspaceChanged
    {
        add => service.Changed += value;
        remove => service.Changed -= value;
    }

    public Task<IReadOnlyList<WorkspaceSnapshot>> LoadWorkspaceListAsync()
        => service.LoadWorkspaceListAsync();

    public Task<WorkspaceSnapshot?> LoadActiveWorkspaceAsync()
        => service.LoadActiveWorkspaceAsync();

    public Task<Result> SwitchActiveWorkspaceAsync(int id)
        => service.SwitchActiveWorkspaceAsync(id);

    public Task<Result> CreateWorkspaceAsync(CreateWorkspaceRequest request)
        => service.CreateWorkspaceAsync(request);

    public Task<Result> RenameWorkspaceAsync(RenameWorkspaceRequest request)
        => service.RenameWorkspaceAsync(request);

    public Task<Result> DeleteWorkspaceAsync(int id)
        => service.DeleteWorkspaceAsync(id);
}
