using Models.HelixStudio;

namespace HelixStudio.Logic;

/// <summary>工具目錄這一頁：把 view 的欄位擺到畫面上</summary>
/// <remarks>過濾、計數、排序、文字都在 view 與查詢裡做完；這裡只把欄位搬到畫面要的格子。</remarks>
public static class CatalogLogic
{
    #region 打開工具目錄

    /// <summary>打開工具目錄：左邊的清單、選中的那一支、找不到時那一段</summary>
    /// <param name="rows">vw_catalog：總數、照搜尋字查出來的工具、搜尋過的字</param>
    /// <param name="icons">圖示名稱對到的每一段路徑</param>
    /// <param name="query">搜尋框打的字</param>
    /// <param name="loaded">資料讀完了沒有</param>
    /// <param name="slow">讀超過 300 毫秒還沒回來</param>
    /// <param name="picked">選中的工具代碼；還沒選過就是空字串</param>
    public static CatalogView Catalog(IReadOnlyList<vw_catalog> rows, Dictionary<string, List<string>> icons, string query,
        bool loaded, bool slow, string picked)
    {
        // 全部幾支；還沒讀到就是零
        var summary = rows.FirstOrDefault(row => row.type == "total") ?? new() { tool_count = 0 };

        // 清單：view 已經照最近開過的排好
        var tools = rows
            .Where(row => row.type == "tool")
            .OrderBy(row => row.sort)
            .Select(row => new RowItem
            {
                Key = $"{row.tool_no}",
                Name = $"{row.name}",
                Paths = icons[$"{row.icon}"],
                Vendor = $"{row.vendor}",
                Version = $"{row.version_text}",
                LastOpen = row.last_open_text,
                Opens = row.opens ?? 0,
                Autostart = row.autostart == true,
            })
            .ToList();

        // 選中的那一支：選過的還在清單裡就留著，不在了就落到第一支
        var chosen = tools.Select(one => one.Key).Where(one => one == picked).DefaultIfEmpty(tools.Select(one => one.Key).FirstOrDefault() ?? string.Empty).First();

        return new()
        {
            Summary = summary,
            Query = query,
            Recents = rows.Where(row => row.type == "search").Select(row => $"{row.term}").ToList(),
            Rows = tools,
            Picked = chosen,
            Chosen = tools.FindIndex(one => one.Key == chosen),

            // 有工具但這一次沒有一支符合
            Empties = Enumerable.Repeat(new EmptyView { Query = query, Paths = icons["search"] }, 1)
                .Where(_ => loaded && summary.tool_count > 0 && tools.Count == 0)
                .ToList(),

            // 一支工具都還沒有：第一次用這台裝置
            Firsts = Enumerable.Repeat(icons["package"], 1).Where(_ => loaded && summary.tool_count == 0).ToList(),

            // 讀太久才顯示骨架，300 毫秒以內讀完就什麼都不顯示
            Skeletons = Enumerable.Range(0, Skeleton).Where(_ => slow && !loaded).ToList(),
        };
    }

    #endregion

    #region 對照

    /// <summary>骨架擺幾列</summary>
    private const int Skeleton = 6;

    #endregion

    #region 模型

    /// <summary>工具目錄這一頁</summary>
    public class CatalogView
    {
        /// <summary>vw_catalog 的 total 那一列：總共幾支</summary>
        public vw_catalog Summary { get; set; } = new();

        /// <summary>搜尋框現在的字</summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>搜尋過的字，最近的在前</summary>
        public List<string> Recents { get; set; } = [];

        /// <summary>清單的每一列，最近開過的在前</summary>
        public List<RowItem> Rows { get; set; } = [];

        /// <summary>右邊顯示的那一支工具的代碼；一支都沒有就是空字串</summary>
        public string Picked { get; set; } = string.Empty;

        /// <summary>選中的是清單第幾列；一支都沒有就是 -1</summary>
        public int Chosen { get; set; }

        /// <summary>一支都不符合時的那一段；有工具就是零筆</summary>
        public List<EmptyView> Empties { get; set; } = [];

        /// <summary>一支工具都還沒有時那一段的箱子圖示；有工具就是零筆</summary>
        public List<List<string>> Firsts { get; set; } = [];

        /// <summary>還在讀而且讀很久時要擺的骨架列；其餘是零筆</summary>
        public List<int> Skeletons { get; set; } = [];
    }

    /// <summary>清單上的一列：一格一個值</summary>
    public class RowItem
    {
        /// <summary>工具代碼</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>工具名稱</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>圖示的每一段路徑</summary>
        public List<string> Paths { get; set; } = [];

        /// <summary>維護團隊</summary>
        public string Vendor { get; set; } = string.Empty;

        /// <summary>版本的字：vw_catalog.version_text</summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>最後開啟的時刻；沒開過是 null</summary>
        public string? LastOpen { get; set; }

        /// <summary>開過幾次</summary>
        public int Opens { get; set; }

        /// <summary>隨 HelixStudio Demo 自動啟動</summary>
        public bool Autostart { get; set; }
    }

    /// <summary>一支都不符合時顯示的那一段</summary>
    public class EmptyView
    {
        /// <summary>搜尋框的字</summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>放大鏡的每一段路徑</summary>
        public List<string> Paths { get; set; } = [];
    }

    #endregion
}
