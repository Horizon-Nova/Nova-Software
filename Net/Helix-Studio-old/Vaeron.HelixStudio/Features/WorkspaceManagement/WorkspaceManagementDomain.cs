using Vaeron.HelixStudio.Features.AppsManagement;
using Vaeron.HelixStudio.Features.EventLog;
using Vaeron.HelixStudio.Features.WorkspaceManagement.Contracts;
using Vaeron.HelixStudio.Features.WorkspaceManagement.Data;
using Vaeron.SharedKernel;

namespace Vaeron.HelixStudio.Features.WorkspaceManagement;

public sealed class WorkspaceManagementDomain(
    WorkspaceManagementRepository repo,
    EventLogFacade eventLogFacade,
    AppsManagementFacade appsManagementFacade)
{
    #region 事件中繼

    public event Action? Changed;

    #endregion

    #region 統一的查詢方法

    public async Task<IReadOnlyList<WorkspaceSnapshot>> LoadWorkspaceListAsync()
    {
        var activeId = await repo.QueryActiveWorkspaceIdAsync();
        var rows = await repo.QueryWorkspaceListAsync();
        return rows.Select(x => ToSnapshot(x, x.id == activeId)).ToList();
    }

    public async Task<WorkspaceSnapshot?> LoadActiveWorkspaceAsync()
    {
        var activeId = await repo.QueryActiveWorkspaceIdAsync();
        if (activeId is null) return null;
        var row = await repo.QueryWorkspaceAsync(activeId.Value);
        return row is null ? null : ToSnapshot(row, isActive: true);
    }

    #endregion

    #region 基本 CRUD 操作

    public async Task<(bool success, string message)> CreateWorkspaceAsync(CreateWorkspaceRequest request)
    {
        var (ok, msg) = await ValidateNameAsync(request.Name, excludeId: null);
        if (!ok) return (false, msg);

        await repo.InsertWorkspaceAndActivateIfFirstAsync(new workspace
        {
            name = request.Name,
            description = request.Description,
            created_at = DateTimeOffset.UtcNow,
        });

        Changed?.Invoke();
        return ResultContract.Ok("已建立");
    }

    public async Task<(bool success, string message)> RenameWorkspaceAsync(RenameWorkspaceRequest request)
    {
        var (ok, msg) = await ValidateNameAsync(request.Name, request.Id);
        if (!ok) return (false, msg);

        var updated = await repo.InsertWorkspaceAsync(new workspace
        {
            id = request.Id,
            name = request.Name,
            description = request.Description,
        });
        if (updated == 0) return ResultContract.Fail("找不到該工作區");

        Changed?.Invoke();
        return ResultContract.Ok("已更新");
    }

    public async Task<(bool success, string message)> DeleteWorkspaceAsync(int id)
    {
        var count = await repo.CountWorkspacesAsync();
        if (count <= 1) return ResultContract.Fail("至少要保留一個工作區");

        var deleted = await repo.DeleteWorkspaceAndReassignActiveAsync(id);
        if (!deleted) return ResultContract.Fail("找不到該工作區");

        await eventLogFacade.ApplyDeleteEventsByWorkspaceAsync(id);
        await appsManagementFacade.ApplyDeleteToolsByWorkspaceAsync(id);

        Changed?.Invoke();
        return ResultContract.Ok("已刪除");
    }

    public async Task<(bool success, string message)> SwitchActiveWorkspaceAsync(int id)
    {
        var target = await repo.QueryWorkspaceAsync(id);
        if (target is null) return ResultContract.Fail("找不到該工作區");

        await repo.InsertActiveWorkspaceAsync(id);
        Changed?.Invoke();
        return ResultContract.Ok("已切換");
    }

    #endregion

    #region 驗證方法

    private async Task<(bool ok, string msg)> ValidateNameAsync(string name, int? excludeId)
        => await repo.ExistsWorkspaceAsync(name, excludeId)
            ? (false, $"{ResultContract.FailPrefix}名稱已存在")
            : (true, "");

    #endregion

    private static WorkspaceSnapshot ToSnapshot(workspace x, bool isActive) => new()
    {
        Id = x.id,
        Name = x.name,
        Description = x.description,
        CreatedAt = x.created_at,
        IsActive = isActive,
    };
}
