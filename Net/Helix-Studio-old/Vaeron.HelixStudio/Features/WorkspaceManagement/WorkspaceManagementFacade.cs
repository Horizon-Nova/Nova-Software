using Vaeron.HelixStudio.Features.WorkspaceManagement.Contracts;

namespace Vaeron.HelixStudio.Features.WorkspaceManagement;

/// <summary>供其他 Feature（MainShell／AppsManagement／EventLog／Settings...）取得工作區資料、
/// 下工作區操作命令的唯一入口，不外洩 Repository／Domain 內部型別。</summary>
public sealed class WorkspaceManagementFacade(WorkspaceManagementDomain domain)
{
    public event Action? WorkspaceChanged
    {
        add => domain.Changed += value;
        remove => domain.Changed -= value;
    }

    public Task<IReadOnlyList<WorkspaceSnapshot>> LoadWorkspaceListAsync()
        => domain.LoadWorkspaceListAsync();

    public Task<WorkspaceSnapshot?> LoadActiveWorkspaceAsync()
        => domain.LoadActiveWorkspaceAsync();

    public Task<(bool success, string message)> SwitchActiveWorkspaceAsync(int id)
        => domain.SwitchActiveWorkspaceAsync(id);

    public Task<(bool success, string message)> CreateWorkspaceAsync(CreateWorkspaceRequest request)
        => domain.CreateWorkspaceAsync(request);

    public Task<(bool success, string message)> RenameWorkspaceAsync(RenameWorkspaceRequest request)
        => domain.RenameWorkspaceAsync(request);

    public Task<(bool success, string message)> DeleteWorkspaceAsync(int id)
        => domain.DeleteWorkspaceAsync(id);
}
