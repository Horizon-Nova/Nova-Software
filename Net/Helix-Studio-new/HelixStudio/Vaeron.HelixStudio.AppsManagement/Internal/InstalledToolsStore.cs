using Microsoft.EntityFrameworkCore;
using Vaeron.HelixStudio.AppsManagement.Internal.Models;

namespace Vaeron.HelixStudio.AppsManagement.Internal;

internal sealed record ExistsByNameQuery(int WorkspaceId, string Name);

internal sealed class InstalledToolsStore(IDbContextFactory<AppsManagementDbContext> dbFactory)
{
    private static IQueryable<InstalledToolRow> Filter(AppsManagementDbContext db)
        => db.installed_tools.Where(x => !x.is_deleted);

    private static IQueryable<InstalledToolRow> Filter(AppsManagementDbContext db, int workspaceId)
        => Filter(db).Where(x => x.workspace_id == workspaceId);

    public async Task<List<InstalledToolRow>> QueryInstalledToolListAsync(int workspaceId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Filter(db, workspaceId).OrderBy(x => x.name).ToListAsync();
    }

    public async Task<InstalledToolRow?> QueryInstalledToolAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Filter(db).FirstOrDefaultAsync(x => x.id == id);
    }

    public async Task<bool> ExistsByNameAsync(ExistsByNameQuery query)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Filter(db, query.WorkspaceId).AnyAsync(x => x.name == query.Name);
    }

    public async Task<int> InsertInstalledToolAsync(InstalledToolRow data)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (data.id == 0)
        {
            db.installed_tools.Add(data);
            await db.SaveChangesAsync();
            return data.id;
        }

        var existing = await db.installed_tools.FirstOrDefaultAsync(x => x.id == data.id);
        if (existing is null) return 0;
        existing.auto_start = data.auto_start;
        existing.auto_repair = data.auto_repair;
        existing.locked_fields_json = data.locked_fields_json;
        await db.SaveChangesAsync();
        return existing.id;
    }

    public async Task<bool> DeleteInstalledToolAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.installed_tools.FirstOrDefaultAsync(x => x.id == id);
        if (existing is null) return false;
        existing.is_deleted = true;
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>刪除工作區時連動清理已安裝工具</summary>
    public async Task DeleteToolsByWorkspaceAsync(int workspaceId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await Filter(db, workspaceId).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.is_deleted, true));
    }
}
