using Microsoft.EntityFrameworkCore;
using Models.HelixStudio;

namespace HelixStudio.Store;

/// <summary>日誌這個主題唯一碰資料庫的型別</summary>
/// <remarks>讀走 vw_event_log 一支，寫才碰 event_log；每一次呼叫現查。開啟、啟用、解鎖、錯誤都由各自的 Service 呼叫這裡寫一筆。</remarks>
public class LogStore
{
    #region 統一的查詢來源

    /// <summary>平台的資料庫</summary>
    private HelixStudioDbContext Db { get; } = new(
        new DbContextOptionsBuilder<HelixStudioDbContext>()
            .UseNpgsql(new SecretStore().Secret("HelixStudio"))
            .Options);

    /// <summary>日誌讀得到的每一列：每一種事件的筆數與每一件事</summary>
    private IQueryable<vw_event_log> ValidRows() =>
        Db.vw_event_logs.AsNoTracking();

    #endregion

    #region 專用查詢方法

    /// <summary>單一：每一種事件的筆數，與編號完全相同的那一件</summary>
    /// <param name="id">那一件的編號</param>
    public List<vw_event_log> QueryEventLogItem(int id) =>
        [.. ValidRows().Where(row => row.type == "count" || row.id == id)];

    /// <summary>列表：每一種事件的筆數，與訊息、工具、方法、帳號或呼叫堆疊裡有那個字的每一件（沒填就是全部），新的在前</summary>
    /// <param name="keyword">搜尋框打的字</param>
    public List<vw_event_log> QueryEventLogItems(string keyword) =>
        [.. ValidRows()
            .Where(row => row.type == "count"
                || keyword == string.Empty
                || EF.Functions.ILike(row.message!, $"%{keyword}%")
                || EF.Functions.ILike(row.tool_name!, $"%{keyword}%")
                || EF.Functions.ILike(row.function_name!, $"%{keyword}%")
                || EF.Functions.ILike(row.account!, $"%{keyword}%")
                || EF.Functions.ILike(row.stack_trace!, $"%{keyword}%"))
            .OrderBy(row => row.type)
            .ThenBy(row => row.sort)];

    #endregion

    #region 基本 CRUD 操作

    /// <summary>event_log 唯一的寫入：只新增不改寫，編號是零的新增</summary>
    /// <param name="rows">要寫進去的每一件</param>
    public void InsertEventLogList(IReadOnlyList<event_log> rows)
    {
        Db.event_logs.AddRange(rows.Where(row => row.id == 0));

        Db.SaveChanges();
    }

    #endregion
}
