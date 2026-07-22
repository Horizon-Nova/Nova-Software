using Microsoft.EntityFrameworkCore;

namespace Vaeron.HelixStudio.Features.RemoteBatchTransfer.Data;

public sealed class RemoteBatchTransferRepository(IDbContextFactory<RemoteBatchTransferDbContext> dbFactory)
{
    #region 統一的過濾條件

    private static IQueryable<remote_connection> Filter(RemoteBatchTransferDbContext db)
        => db.remote_connections.Where(x => !x.is_deleted);

    #endregion

    #region 專用查詢方法

    public async Task<List<remote_connection>> QueryConnectionListAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Filter(db).OrderBy(x => x.name).ToListAsync();
    }

    public async Task<remote_connection?> QueryConnectionAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Filter(db).FirstOrDefaultAsync(x => x.id == id);
    }

    #endregion

    #region 基本 CRUD 操作

    public async Task<int> InsertConnectionAsync(remote_connection data)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (data.id == 0)
        {
            db.remote_connections.Add(data);
            await db.SaveChangesAsync();
            return data.id;
        }

        var existing = await Filter(db).FirstOrDefaultAsync(x => x.id == data.id);
        if (existing is null) return 0;
        existing.name = data.name;
        existing.host = data.host;
        existing.port = data.port;
        existing.auth_method = data.auth_method;
        existing.account = data.account;
        await db.SaveChangesAsync();
        return existing.id;
    }

    public async Task<bool> DeleteConnectionAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await Filter(db).FirstOrDefaultAsync(x => x.id == id);
        if (existing is null) return false;
        existing.is_deleted = true;
        await db.SaveChangesAsync();
        return true;
    }

    #endregion
}
