using Microsoft.EntityFrameworkCore;
using Models.HelixStudio;

namespace ProcessMonitor.Store;

/// <summary>程式監控這個主題唯一碰資料庫的型別</summary>
/// <remarks>讀走 vw_monitor 一支，寫才碰 monitor；每一次呼叫現查。連線字串由平台交給畫面（IData），畫面再交給這裡。</remarks>
internal class MonitorStore(string connection)
{
    #region 統一的查詢來源

    /// <summary>平台的資料庫</summary>
    private HelixStudioDbContext Db { get; } = new(
        new DbContextOptionsBuilder<HelixStudioDbContext>()
            .UseNpgsql(connection)
            .Options);

    /// <summary>程式監控讀得到的每一列：事件、每一支程式現在的限制、閃退次數、見過的程式與開機自動執行、欄位設定、從什麼時候開始記錄</summary>
    private IQueryable<vw_monitor> ValidRows() =>
        Db.vw_monitors.AsNoTracking();

    #endregion

    #region 專用查詢方法

    /// <summary>單一：路徑完全相同（不分大小寫）的那一支程式的事件、限制、閃退次數、見過與開機自動執行，與欄位設定、從什麼時候開始記錄</summary>
    /// <param name="exePath">那一支程式的路徑</param>
    public List<vw_monitor> QueryMonitorItem(string exePath) =>
        [.. ValidRows().Where(row => row.type == "columns" || row.type == "since" || row.exe_path!.ToLower() == exePath.ToLower())];

    /// <summary>列表：每一列，事件新的在前</summary>
    public List<vw_monitor> QueryMonitorItems() =>
        [.. ValidRows().OrderBy(row => row.type).ThenBy(row => row.sort)];

    #endregion

    #region 基本 CRUD 操作

    /// <summary>monitor 唯一的寫入：編號指得到的列整列換成送來的值（建立時刻不動），編號是零的新增</summary>
    /// <remarks>寫入方法照資料表開，不照功能開；記事件、設限制、記見過、存欄位設定都呼叫這一支。</remarks>
    /// <param name="rows">要寫回去的每一列</param>
    public void InsertMonitorList(IReadOnlyList<monitor> rows)
    {
        var ids = rows.Select(row => row.id).ToList();
        var held = Db.monitors.Where(row => ids.Contains(row.id)).ToList();

        // 指得到的列：送來的值蓋上去，建立時刻不動
        foreach (var pair in held.Join(rows, one => one.id, row => row.id, (one, row) => (Held: one, Sent: row)))
        {
            Db.Entry(pair.Held).CurrentValues.SetValues(pair.Sent);
            Db.Entry(pair.Held).Property(one => one.created_at).IsModified = false;
        }

        // 編號是零的是新的
        Db.monitors.AddRange(rows.Where(row => row.id == 0));

        Db.SaveChanges();
    }

    #endregion
}
