using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vaeron.Kernel.Data;
using Vaeron.Kernel.Data.Models;
using Vaeron.VNCSCP.Internal.Contracts;

namespace Vaeron.VNCSCP.ConnectionManagement.Internal;

/// <summary>連線資料的讀寫邊界。讀一律經 Vw_Connection，寫一律經 connection 表，篩選條件交給 SQL。</summary>
internal sealed class ConnectionManagementStore(IDbContextFactory<VncscpDbContext> dbFactory)
{
    private const string FilteredConnectionSql = """
        SELECT *
        FROM Vw_Connection
        WHERE (@search = ''
               OR name LIKE '%' || @search || '%'
               OR ip   LIKE '%' || @search || '%')
          AND (@only_abnormal = 0 OR is_abnormal = 1)
        ORDER BY group_display_name, name
        """;

    #region 統一的查詢來源

    private static DbSet<ConnectionRow> ConnectionSourceOf(VncscpDbContext db) => db.ConnectionView;

    private static DbSet<ConnectionGroupRow> GroupSourceOf(VncscpDbContext db) => db.ConnectionGroupView;

    #endregion

    #region 專用查詢方法

    internal async Task<IReadOnlyList<ConnectionRow>> QueryConnectionListAsync(
        ConnectionFilter filter, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await ConnectionSourceOf(db)
            .FromSqlRaw(
                FilteredConnectionSql,
                new SqliteParameter("@search", filter.Search.Trim()),
                new SqliteParameter("@only_abnormal", filter.OnlyAbnormal))
            .ToListAsync(cancellationToken);
    }

    /// <summary>指定連線；找不到時序列為空。</summary>
    internal async Task<IReadOnlyList<ConnectionRow>> QueryConnectionByIdAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await ConnectionSourceOf(db).Where(row => row.Id == id).ToListAsync(cancellationToken);
    }

    internal async Task<IReadOnlyList<string>> QueryGroupNameListAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await GroupSourceOf(db).Select(row => row.Name).ToListAsync(cancellationToken);
    }

    #endregion

    #region 基本 CRUD 操作

    internal async Task InsertConnectionAsync(ConnectionEntity entity, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        entity.CreatedAt = DateTimeOffset.UtcNow;
        db.Connections.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>密文為空陣列代表不變更密碼。回傳受影響的列數。</summary>
    internal async Task<int> UpdateConnectionAsync(
        UpdateConnectionRequest request, byte[] passwordCipher, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var updated = await db.Connections
            .Where(row => row.IsDeleted == false && row.Id == request.Id)
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(row => row.Name, request.Name)
                .SetProperty(row => row.Ip, request.Ip)
                .SetProperty(row => row.Port, request.Port)
                .SetProperty(row => row.GroupName, request.GroupName), cancellationToken);

        await db.Connections
            .Where(row => row.IsDeleted == false && row.Id == request.Id && passwordCipher.Length > 0)
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(row => row.PasswordCipher, passwordCipher), cancellationToken);

        return updated;
    }

    internal async Task<int> UpdateStatusAsync(
        int id, ConnectionStatus status, string message, CancellationToken cancellationToken)
    {
        var testedAt = DateTimeOffset.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Connections
            .Where(row => row.IsDeleted == false && row.Id == id)
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(row => row.Status, (int)status)
                .SetProperty(row => row.LastTestedAt, testedAt)
                .SetProperty(row => row.LastTestMessage, message), cancellationToken);
    }

    internal async Task<int> DeleteConnectionAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Connections
            .Where(row => row.IsDeleted == false && row.Id == id)
            .ExecuteUpdateAsync(setter => setter.SetProperty(row => row.IsDeleted, true), cancellationToken);
    }

    internal async Task<int> RenameGroupAsync(RenameGroupRequest request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Connections
            .Where(row => row.IsDeleted == false && row.GroupName == request.OldName)
            .ExecuteUpdateAsync(setter => setter.SetProperty(row => row.GroupName, request.NewName), cancellationToken);
    }

    internal async Task<int> UngroupAsync(string name, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Connections
            .Where(row => row.IsDeleted == false && row.GroupName == name)
            .ExecuteUpdateAsync(setter => setter.SetProperty(row => row.GroupName, (string?)null), cancellationToken);
    }

    #endregion
}
