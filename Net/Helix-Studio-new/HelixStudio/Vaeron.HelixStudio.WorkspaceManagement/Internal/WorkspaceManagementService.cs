using Vaeron.HelixStudio.Internal.Runtime;
using Vaeron.HelixStudio.WorkspaceManagement.Internal.Models;
using Vaeron.Kernel;

namespace Vaeron.HelixStudio.WorkspaceManagement.Internal;

/// <summary>決定操作成功或失敗、失敗屬於哪一種。刪除工作區後下游 Feature（EventLog／AppsManagement）
/// 各自資料的級聯清除不在此處觸發——WorkspaceManagement 是最基礎的 Feature，不得反向依賴任何
/// 依賴它的 Feature（會構成專案參考循環）。級聯改由觸發刪除的呼叫方（Settings 的工作區 CRUD 畫面）
/// 依序呼叫三個 Facade 完成,見 Settings Feature 的對應說明。</summary>
internal sealed class WorkspaceManagementService(WorkspaceManagementStore store, ActiveWorkspaceRuntime activeWorkspace)
{
    public event Action? Changed;

    public async Task<IReadOnlyList<WorkspaceSnapshot>> LoadWorkspaceListAsync()
    {
        var activeId = await store.QueryActiveWorkspaceIdAsync();
        var rows = await store.QueryWorkspaceListAsync();
        return rows.Select(x => ToSnapshot(x, x.id == activeId)).ToList();
    }

    public async Task<WorkspaceSnapshot?> LoadActiveWorkspaceAsync()
    {
        var activeId = await store.QueryActiveWorkspaceIdAsync();
        activeWorkspace.SetCurrent(activeId);
        if (activeId is null) return null;
        var row = await store.QueryWorkspaceAsync(activeId.Value);
        return row is null ? null : ToSnapshot(row, isActive: true);
    }

    public async Task<Result> CreateWorkspaceAsync(CreateWorkspaceRequest request)
    {
        var nameCheck = await ValidateNameAsync(request.Name, excludeId: null);
        if (!nameCheck.IsSuccess) return nameCheck;

        await store.InsertWorkspaceAndActivateIfFirstAsync(new WorkspaceRow
        {
            name = request.Name,
            description = request.Description,
            created_at = DateTimeOffset.UtcNow,
        });

        Changed?.Invoke();
        return Ok("已建立");
    }

    public async Task<Result> RenameWorkspaceAsync(RenameWorkspaceRequest request)
    {
        var nameCheck = await ValidateNameAsync(request.Name, request.Id);
        if (!nameCheck.IsSuccess) return nameCheck;

        var updated = await store.InsertWorkspaceAsync(new WorkspaceRow
        {
            id = request.Id,
            name = request.Name,
            description = request.Description,
        });
        if (updated == 0) return Result.Fail(ResultKind.NotFound, "找不到該工作區");

        Changed?.Invoke();
        return Ok("已更新");
    }

    public async Task<Result> DeleteWorkspaceAsync(int id)
    {
        var count = await store.CountWorkspacesAsync();
        if (count <= 1) return Result.Fail(ResultKind.ValidationFailed, "至少要保留一個工作區");

        var deleted = await store.DeleteWorkspaceAndReassignActiveAsync(id);
        if (!deleted) return Result.Fail(ResultKind.NotFound, "找不到該工作區");

        Changed?.Invoke();
        return Ok("已刪除");
    }

    public async Task<Result> SwitchActiveWorkspaceAsync(int id)
    {
        var target = await store.QueryWorkspaceAsync(id);
        if (target is null) return Result.Fail(ResultKind.NotFound, "找不到該工作區");

        await store.InsertActiveWorkspaceAsync(id);
        activeWorkspace.SetCurrent(id);
        Changed?.Invoke();
        return Ok("已切換");
    }

    private async Task<Result> ValidateNameAsync(string name, int? excludeId)
        => await store.ExistsWorkspaceAsync(name, excludeId)
            ? Result.Fail(ResultKind.Conflict, "名稱已存在")
            : Result.Ok();

    private static Result Ok(string message) => new() { Kind = ResultKind.Success, Message = message };

    private static WorkspaceSnapshot ToSnapshot(WorkspaceRow x, bool isActive) => new()
    {
        Id = x.id,
        Name = x.name,
        Description = x.description,
        CreatedAt = x.created_at,
        IsActive = isActive,
    };
}
