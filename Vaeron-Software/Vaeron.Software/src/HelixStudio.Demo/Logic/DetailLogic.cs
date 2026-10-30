using Models.HelixStudio;

namespace HelixStudio.Logic;

/// <summary>工具目錄右邊那一欄：選中那一支工具的資訊</summary>
/// <remarks>版本文字、日期文字、能力與權限、更新日誌都在 view 裡做完；這裡只把它們放進同一欄。</remarks>
public static class DetailLogic
{
    #region 看一支工具的資訊

    /// <summary>看一支工具的資訊</summary>
    /// <param name="rows">vw_catalog 裡這一支工具的 tool 那一列與每一則 change</param>
    /// <param name="icons">圖示名稱對到的每一段路徑</param>
    /// <param name="selected">看的是第幾個分頁：0 總覽、1 操作說明、2 更新日誌</param>
    /// <param name="zoom">放大看的那一張截圖的完整路徑；沒有放大就是空字串</param>
    public static DetailView Detail(IReadOnlyList<vw_catalog> rows, Dictionary<string, List<string>> icons, int selected, string zoom) =>
        new()
        {
            Tool = rows.First(row => row.type == "tool"),
            Changes = rows.Where(row => row.type == "change").OrderBy(row => row.sort).ToList(),
            Steps = rows.Where(row => row.type == "step").OrderBy(row => row.sort).ToList(),
            Selected = selected,
            Zooms = new[] { zoom }.Where(one => one.Length > 0).ToList(),
            Paths = icons[$"{rows.First(row => row.type == "tool").icon}"],
        };

    #endregion

    #region 模型

    /// <summary>右邊那一欄</summary>
    public class DetailView
    {
        /// <summary>vw_catalog 的 tool 那一列</summary>
        public vw_catalog Tool { get; set; } = new();

        /// <summary>vw_catalog 的 change 每一則，新到舊</summary>
        public List<vw_catalog> Changes { get; set; } = [];

        /// <summary>vw_catalog 的 step 每一步，照順序</summary>
        public List<vw_catalog> Steps { get; set; } = [];

        /// <summary>看的是第幾個分頁</summary>
        public int Selected { get; set; }

        /// <summary>放大看的那一張截圖；沒有放大就是零筆</summary>
        public List<string> Zooms { get; set; } = [];

        /// <summary>工具圖示的每一段路徑</summary>
        public List<string> Paths { get; set; } = [];
    }

    #endregion
}
