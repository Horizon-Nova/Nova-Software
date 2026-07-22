using Microsoft.EntityFrameworkCore;

namespace Vaeron.HelixStudio.Features.EventLog.Data;

public sealed class EventLogRepository(IDbContextFactory<EventLogDbContext> dbFactory)
{
    #region 統一的過濾條件

    private static IQueryable<event_entry> Filter(EventLogDbContext db, int workspaceId)
        => db.events.Where(x => x.workspace_id == workspaceId);

    #endregion

    #region 專用查詢方法

    public async Task<List<event_entry>> QueryEventListAsync(int workspaceId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Filter(db, workspaceId).OrderByDescending(x => x.timestamp).ToListAsync();
    }

    public async Task<log_settings> QueryLogSettingsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.log_settings_rows.FirstOrDefaultAsync(x => x.id == 1)
            ?? new log_settings { id = 1, min_level = "Info", retention_days = 30, export_path = null };
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

    #endregion

    #region 基本 CRUD 操作

    public async Task InsertEventAsync(event_entry data)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.events.Add(data);
        await db.SaveChangesAsync();
    }

    public async Task InsertLogSettingsAsync(log_settings data)
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
            db.read_markers.Add(new read_marker { workspace_id = workspaceId, last_read_at = readAt });
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

    #endregion
}
