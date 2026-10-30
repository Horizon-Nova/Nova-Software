using Microsoft.EntityFrameworkCore;
using Models.HelixStudio;

namespace HelixStudio.Store;

/// <summary>指令面板這個主題唯一碰資料庫的型別</summary>
/// <remarks>讀走 vw_palette 一支；面板自己不寫（選過的工具記成搜尋字由 CatalogStore 寫 search）。</remarks>
public class PaletteStore
{
    #region 統一的查詢來源

    /// <summary>平台的資料庫</summary>
    private HelixStudioDbContext Db { get; } = new(
        new DbContextOptionsBuilder<HelixStudioDbContext>()
            .UseNpgsql(new SecretStore().Secret("HelixStudio"))
            .Options);

    /// <summary>面板讀得到的每一列：每一支還裝著的工具與搜尋過的字</summary>
    private IQueryable<vw_palette> ValidRows() =>
        Db.vw_palettes.AsNoTracking();

    #endregion

    #region 專用查詢方法

    /// <summary>單一：代碼完全相同的那一支工具</summary>
    /// <param name="toolNo">那一支工具的代碼</param>
    public List<vw_palette> QueryPaletteItem(string toolNo) =>
        [.. ValidRows().Where(row => row.tool_no == toolNo)];

    /// <summary>列表：每一支還裝著的工具與搜尋過的字，先後 view 已經排好</summary>
    public List<vw_palette> QueryPaletteItems() =>
        [.. ValidRows()];

    #endregion
}
