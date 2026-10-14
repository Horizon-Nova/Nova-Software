using Microsoft.EntityFrameworkCore;
using Vaeron.Kernel.Data;
using Vaeron.Kernel.Data.Models;

namespace Vaeron.VNCSCP.RemoteDeployment.Internal;

/// <summary>部署歷史的讀寫邊界。讀一律經 Vw_DeploymentHistory，寫一律經 deployment_history 表。</summary>
internal sealed class RemoteDeploymentStore(IDbContextFactory<VncscpDbContext> dbFactory)
{
    private const int HistoryLimit = 500;

    #region 統一的查詢來源

    private static DbSet<DeploymentHistoryRow> HistorySourceOf(VncscpDbContext db) => db.DeploymentHistoryView;

    #endregion

    #region 專用查詢方法

    internal async Task<IReadOnlyList<DeploymentHistoryRow>> QueryHistoryListAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await HistorySourceOf(db)
            .OrderByDescending(row => row.DeployedTicks)
            .Take(HistoryLimit)
            .ToListAsync(cancellationToken);
    }

    #endregion

    #region 基本 CRUD 操作

    internal async Task InsertHistoryAsync(DeploymentHistoryEntity entity, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.DeploymentHistories.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    #endregion
}

