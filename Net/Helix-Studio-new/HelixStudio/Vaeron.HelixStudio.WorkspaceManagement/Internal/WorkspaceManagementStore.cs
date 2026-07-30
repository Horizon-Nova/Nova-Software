using Microsoft.EntityFrameworkCore;
using Vaeron.HelixStudio.WorkspaceManagement.Internal.Models;

namespace Vaeron.HelixStudio.WorkspaceManagement.Internal;

/// <summary>資料存取邊界。只做讀寫,不含業務判斷,不 catch——例外傳播給 Service 決定。</summary>
internal sealed class WorkspaceManagementStore(IDbContextFactory<WorkspaceManagementDbContext> dbFactory)
{
    private static IQueryable<WorkspaceRow> Filter(WorkspaceManagementDbContext db)
        => db.workspaces.Where(x => !x.is_deleted);

    public async Task<List<WorkspaceRow>> QueryWorkspaceListAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Filter(db).OrderBy(x => x.name).ToListAsync();
    }

    public async Task<WorkspaceRow?> QueryWorkspaceAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Filter(db).FirstOrDefaultAsync(x => x.id == id);
    }

    public async Task<bool> ExistsWorkspaceAsync(string name, int? excludeId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Filter(db).AnyAsync(x => x.name == name && x.id != excludeId);
    }

    public async Task<int> CountWorkspacesAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Filter(db).CountAsync();
    }

    public async Task<int?> QueryActiveWorkspaceIdAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var row = await db.active_workspaces.FirstOrDefaultAsync(x => x.id == 1);
        return row?.workspace_id;
    }

    public async Task<int> InsertWorkspaceAsync(WorkspaceRow data)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (data.id == 0)
        {
            db.workspaces.Add(data);
        }
        else
        {
            var existing = await Filter(db).FirstOrDefaultAsync(x => x.id == data.id);
            if (existing is null) return 0;
            existing.name = data.name;
            existing.description = data.description;
        }
        await db.SaveChangesAsync();
        return data.id;
    }

    public async Task InsertActiveWorkspaceAsync(int workspaceId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var row = await db.active_workspaces.FirstOrDefaultAsync(x => x.id == 1);
        if (row is null)
            db.active_workspaces.Add(new ActiveWorkspaceRow { id = 1, workspace_id = workspaceId });
        else
            row.workspace_id = workspaceId;
        await db.SaveChangesAsync();
    }

    /// <summary>新增工作區＋首筆自動設為作用中（單一交易）</summary>
    public async Task<int> InsertWorkspaceAndActivateIfFirstAsync(WorkspaceRow data)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        db.workspaces.Add(data);
        await db.SaveChangesAsync();

        var count = await Filter(db).CountAsync();
        if (count == 1)
        {
            var row = await db.active_workspaces.FirstOrDefaultAsync(x => x.id == 1);
            if (row is null)
                db.active_workspaces.Add(new ActiveWorkspaceRow { id = 1, workspace_id = data.id });
            else
                row.workspace_id = data.id;
            await db.SaveChangesAsync();
        }

        await tx.CommitAsync();
        return data.id;
    }

    /// <summary>刪除工作區＋重新指派作用中工作區（單一交易）</summary>
    public async Task<bool> DeleteWorkspaceAndReassignActiveAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var existing = await Filter(db).FirstOrDefaultAsync(x => x.id == id);
        if (existing is null) return false;
        existing.is_deleted = true;
        await db.SaveChangesAsync();

        var activeRow = await db.active_workspaces.FirstOrDefaultAsync(x => x.id == 1);
        if (activeRow is not null && activeRow.workspace_id == id)
        {
            var remaining = await Filter(db).OrderBy(x => x.name).FirstOrDefaultAsync();
            if (remaining is not null)
                activeRow.workspace_id = remaining.id;
            await db.SaveChangesAsync();
        }

        await tx.CommitAsync();
        return true;
    }
}
