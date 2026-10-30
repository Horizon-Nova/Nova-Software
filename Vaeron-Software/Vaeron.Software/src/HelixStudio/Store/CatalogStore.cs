using Microsoft.EntityFrameworkCore;
using Models.HelixStudio;

namespace HelixStudio.Store;

/// <summary>工具目錄這個主題唯一碰資料庫的型別</summary>
/// <remarks>讀走 vw_catalog 一支，寫才碰 search；每一次呼叫現查。</remarks>
public class CatalogStore
{
    #region 統一的查詢來源

    /// <summary>平台的資料庫</summary>
    private HelixStudioDbContext Db { get; } = new(
        new DbContextOptionsBuilder<HelixStudioDbContext>()
            .UseNpgsql(new SecretStore().Secret("HelixStudio"))
            .Options);

    /// <summary>工具目錄讀得到的每一列：總數、每一支還裝著的工具、更新日誌、操作說明、搜尋過的字</summary>
    private IQueryable<vw_catalog> ValidRows() =>
        Db.vw_catalogs.AsNoTracking();

    #endregion

    #region 專用查詢方法

    /// <summary>單一：代碼完全相同的那一支工具，與它的每一則更新日誌、每一步操作說明</summary>
    /// <param name="toolNo">那一支工具的代碼</param>
    public List<vw_catalog> QueryCatalogItem(string toolNo) =>
        [.. ValidRows().Where(row => row.tool_no == toolNo)];

    /// <summary>列表：總數、搜尋過的字，與名稱、維護團隊或說明裡有那個字的每一支工具（沒填就是全部）</summary>
    /// <param name="keyword">搜尋框打的字</param>
    public List<vw_catalog> QueryCatalogItems(string keyword) =>
        [.. ValidRows()
            .Where(row => row.type != "tool"
                || keyword == string.Empty
                || EF.Functions.ILike(row.name!, $"%{keyword}%")
                || EF.Functions.ILike(row.vendor!, $"%{keyword}%")
                || EF.Functions.ILike(row.description!, $"%{keyword}%"))];

    #endregion

    #region 基本 CRUD 操作

    /// <summary>search 唯一的寫入：編號指得到的列整列換成送來的值（建立時刻不動），編號是零的新增</summary>
    /// <param name="rows">要寫回去的每一列</param>
    public void InsertSearchList(IReadOnlyList<search> rows)
    {
        var ids = rows.Select(row => row.id).ToList();
        var held = Db.searches.Where(row => ids.Contains(row.id)).ToList();

        // 指得到的列：送來的值蓋上去，建立時刻不動
        foreach (var pair in held.Join(rows, one => one.id, row => row.id, (one, row) => (Held: one, Sent: row)))
        {
            Db.Entry(pair.Held).CurrentValues.SetValues(pair.Sent);
            Db.Entry(pair.Held).Property(one => one.created_at).IsModified = false;
        }

        // 編號是零的是新的
        Db.searches.AddRange(rows.Where(row => row.id == 0));

        Db.SaveChanges();
    }

    #endregion
}
