using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vaeron.Kernel.Data;
using Vaeron.Kernel.Data.Models;
using Vaeron.VNCSCP.Internal.Contracts;

namespace Vaeron.VNCSCP.EventLog.Internal;

/// <summary>事件資料的讀寫邊界。讀一律經 Vw_EventLog，篩選條件交給 SQL。</summary>
internal sealed class EventLogStore(IDbContextFactory<VncscpDbContext> dbFactory)
{
    private const string FilteredEventSql = """
        SELECT *
        FROM Vw_EventLog
        WHERE (@search = ''
               OR source  LIKE '%' || @search || '%'
               OR message LIKE '%' || @search || '%')
          AND (@level = 'All'
               OR level = CASE @level
                            WHEN 'Info'    THEN 0
                            WHEN 'Warning' THEN 1
                            WHEN 'Error'   THEN 2
                            ELSE -1
                          END)
          AND (@range = 'All'
               OR occurred_utc_ticks >= CASE @range
                    WHEN 'Today'  THEN strftime('%s', 'now', 'localtime', 'start of day', 'utc') * 10000000 + 621355968000000000
                    WHEN 'Last7'  THEN strftime('%s', 'now', '-7 days')  * 10000000 + 621355968000000000
                    WHEN 'Last30' THEN strftime('%s', 'now', '-30 days') * 10000000 + 621355968000000000
                    ELSE 0
                  END)
        ORDER BY occurred_utc_ticks DESC
        LIMIT 500
        """;

    #region 統一的查詢來源

    private static DbSet<EventLogRow> EventSourceOf(VncscpDbContext db) => db.EventLogView;

    private static DbSet<ConnectionRecentEventRow> RecentEventSourceOf(VncscpDbContext db) => db.ConnectionRecentEventView;

    #endregion

    #region 專用查詢方法

    internal async Task<IReadOnlyList<EventLogRow>> QueryEventListAsync(
        EventFilter filter, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await EventSourceOf(db)
            .FromSqlRaw(
                FilteredEventSql,
                new SqliteParameter("@search", filter.Search.Trim()),
                new SqliteParameter("@level", filter.Level),
                new SqliteParameter("@range", filter.TimeRange))
            .ToListAsync(cancellationToken);
    }

    /// <summary>多筆連線各自的近期事件。每台取幾筆由 Vw_ConnectionRecentEvent 決定。</summary>
    internal async Task<IReadOnlyList<ConnectionRecentEventRow>> QueryRecentEventListAsync(
        IReadOnlyList<int> connectionIds, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await RecentEventSourceOf(db)
            .Where(row => connectionIds.Contains(row.RelatedConnectionId))
            .OrderByDescending(row => row.OccurredUtcTicks)
            .ToListAsync(cancellationToken);
    }

    #endregion

    #region 基本 CRUD 操作

    internal async Task InsertEventAsync(EventLogEntity entity, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.EventLogs.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    #endregion
}
