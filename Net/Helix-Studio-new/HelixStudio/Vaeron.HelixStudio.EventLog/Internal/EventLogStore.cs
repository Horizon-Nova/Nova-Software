using Microsoft.EntityFrameworkCore;
using Vaeron.HelixStudio.EventLog.Internal.Models;

namespace Vaeron.HelixStudio.EventLog.Internal;

internal sealed class EventLogStore(IDbContextFactory<EventLogDbContext> dbFactory)
{
    private static IQueryable<EventEntryRow> Filter(EventLogDbContext db, int workspaceId)
        => db.events.Where(x => x.workspace_id == workspaceId);

    public async Task<List<EventEntryRow>> QueryEventListAsync(int workspaceId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Filter(db, workspaceId).OrderByDescending(x => x.timestamp).ToListAsync();
    }

    public async Task<LogSettingsRow> QueryLogSettingsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.log_settings_rows.FirstOrDefaultAsync(x => x.id == 1)
            ?? new LogSettingsRow { id = 1, min_level = "Info", retention_days = 30, export_path = null };
    }

    public async Task<DateTimeOffset?> QueryLastReadAtAsync(int workspaceId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var row = await db.read_markers.FirstOrDefaultAsync(x => x.workspace_id == workspaceId);
        return row?.last_read_at;
    }

    public async Task<int> CountUnreadAsync(int workspaceId, DateTimeOffset? since)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = Filter(db, workspaceId);
        if (since is not null) query = query.Where(x => x.timestamp > since.Value);
        return await query.CountAsync();
    }

    public async Task InsertEventAsync(EventEntryRow data)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.events.Add(data);
        await db.SaveChangesAsync();
    }

    public async Task InsertLogSettingsAsync(LogSettingsRow data)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.log_settings_rows.FirstOrDefaultAsync(x => x.id == 1);
        if (existing is null)
        {
            data.id = 1;
            db.log_settings_rows.Add(data);
        }
        else
        {
            existing.min_level = data.min_level;
            existing.retention_days = data.retention_days;
            existing.export_path = data.export_path;
        }
        await db.SaveChangesAsync();
    }

    public async Task InsertReadMarkerAsync(int workspaceId, DateTimeOffset readAt)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.read_markers.FirstOrDefaultAsync(x => x.workspace_id == workspaceId);
        if (existing is null)
            db.read_markers.Add(new ReadMarkerRow { workspace_id = workspaceId, last_read_at = readAt });
        else
            existing.last_read_at = readAt;
        await db.SaveChangesAsync();
    }

    /// <summary>刪除工作區時連動清理事件與已讀標記</summary>
    public async Task DeleteEventsByWorkspaceAsync(int workspaceId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await Filter(db, workspaceId).ExecuteDeleteAsync();
        await db.read_markers.Where(x => x.workspace_id == workspaceId).ExecuteDeleteAsync();
    }

    /// <summary>記錄保留期限清理</summary>
    public async Task<int> DeleteEventsOlderThanAsync(DateTimeOffset cutoff)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.events.Where(x => x.timestamp < cutoff).ExecuteDeleteAsync();
    }
}
