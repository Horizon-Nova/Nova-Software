using Microsoft.EntityFrameworkCore;
using Vaeron.HelixStudio.RemoteBatchTransfer.Internal.Models;

namespace Vaeron.HelixStudio.RemoteBatchTransfer.Internal;

internal sealed class RemoteBatchTransferStore(IDbContextFactory<RemoteBatchTransferDbContext> dbFactory)
{
    private static IQueryable<RemoteConnectionRow> Filter(RemoteBatchTransferDbContext db)
        => db.remote_connections.Where(x => !x.is_deleted);

    public async Task<List<RemoteConnectionRow>> QueryConnectionListAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Filter(db).OrderBy(x => x.name).ToListAsync();
    }

    public async Task<RemoteConnectionRow?> QueryConnectionAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Filter(db).FirstOrDefaultAsync(x => x.id == id);
    }

    public async Task<int> InsertConnectionAsync(RemoteConnectionRow data)
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
}
