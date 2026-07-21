using Microsoft.EntityFrameworkCore;

namespace Vaeron.HelixStudio.Features.AppsManagement.Data;

public sealed class InstalledToolsRepository(IDbContextFactory<AppsManagementDbContext> dbFactory)
{
    #region 統一的過濾條件

    private static IQueryable<installed_tool> Filter(AppsManagementDbContext db)
        => db.installed_tools.Where(x => !x.is_deleted);

    private static IQueryable<installed_tool> Filter(AppsManagementDbContext db, int workspaceId)
        => Filter(db).Where(x => x.workspace_id == workspaceId);

    #endregion

    #region 專用查詢方法

    public async Task<List<installed_tool>> QueryInstalledToolListAsync(int workspaceId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Filter(db, workspaceId).OrderBy(x => x.name).ToListAsync();
    }

    public async Task<installed_tool?> QueryInstalledToolAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Filter(db).FirstOrDefaultAsync(x => x.id == id);
    }

    public async Task<bool> ExistsByNameAsync(int workspaceId, string name)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Filter(db, workspaceId).AnyAsync(x => x.name == name);
    }

    #endregion

    #region 基本 CRUD 操作

    public async Task<int> InsertInstalledToolAsync(installed_tool data)
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

    #endregion
}
