using Models.HelixStudio;

namespace HelixStudio.Logic;

/// <summary>指令面板這一頁的純運算</summary>
/// <remarks>不碰資料庫也不碰畫面，給什麼算什麼；沒有狀態，所以是靜態的。</remarks>
public static class PaletteLogic
{
    #region 打開指令面板

    /// <summary>打開指令面板：動作與工具分組列出，命中的字標色</summary>
    /// <param name="rows">vw_palette：每一支工具與搜尋過的字（最近的在前），每一列都帶工具總數</param>
    /// <param name="icons">圖示名稱對到的每一段路徑</param>
    /// <param name="query">搜尋框打的字</param>
    public static PaletteView Palette(IReadOnlyList<vw_palette> rows, Dictionary<string, List<string>> icons, string query)
    {
        var word = query.Trim();
        var searches = rows.Where(row => row.type == "search").ToList();

        // 每一支工具一列：說明是維護團隊
        var toolItems = rows
            .Where(row => row.type == "tool")
            .Select(row => new PaletteItem
            {
                Paths = icons[$"{row.icon}"],
                Label = $"{row.name}",
                Hint = $"{row.vendor}",
                Tag = "開啟",
                Remembers = [$"{row.name}"],
                Opens = [$"{row.tool_no}"],
            })
            .ToList();

        // 動作那幾列
        var actionItems = new List<PaletteItem>
        {
            new()
            {
                Paths = icons["layout-grid"],
                Label = "工具目錄",
                Hint = $"看全部 {rows.Select(row => row.tool_count).FirstOrDefault() ?? 0} 支工具",
                Catalogs = [true],
            },
            new()
            {
                Paths = icons["settings"],
                Label = "開啟設定",
                Hint = "啟動、更新、現場操作",
                Settings = [true],
            },
        };

        // 關鍵字比標題與說明
        var hitTools = toolItems.Where(one => Matches(one, word)).ToList();
        var hitActions = actionItems.Where(one => Matches(one, word)).ToList();

        // 最近搜尋過的字對到的工具，最近的在前；同樣要通過關鍵字
        var recentTools = searches
            .SelectMany(one => toolItems.Where(two => two.Label.Contains($"{one.term}")).Take(1))
            .ToList();
        var recentItems = recentTools.Where(one => Matches(one, word)).ToList();

        // 每一組打中的比例就是它有多貼近這次搜尋，比例高的排前面；一列都沒有的那一組不出現
        var groups = Enumerable
            .Repeat(new GroupItem { Label = "最近使用", Rank = Share(recentItems.Count, recentTools.Count), Items = recentItems }, 1)
            .Concat(Enumerable.Repeat(new GroupItem { Label = "動作", Rank = Share(hitActions.Count, actionItems.Count), Items = hitActions }, 1))
            .Concat(Enumerable.Repeat(new GroupItem { Label = "工具", Rank = Share(hitTools.Count, toolItems.Count), Items = hitTools }, 1))
            .Where(one => one.Items.Count > 0)
            .OrderByDescending(one => one.Rank)
            .Select(one => new GroupItem { Label = one.Label, Rank = one.Rank, Items = one.Items.Select(two => Lit(two, word)).ToList() })
            .ToList();

        return new PaletteView
        {
            Query = query,
            Hint = "搜尋工具或動作…",
            EscLabel = "Esc",
            SearchPaths = icons["search"],
            Groups = groups,
            Empties = Enumerable.Repeat(new EmptyView
            {
                Title = $"沒有叫「{word}」的工具或動作",
                Text = $"試著搜工具名稱，或看看工具目錄裡的 {rows.Select(row => row.tool_count).FirstOrDefault() ?? 0} 支工具。",
            }, 1).Where(_ => groups.Count == 0).ToList(),
        };
    }

    /// <summary>這一組被打中的比例：打中幾個除以本來有幾個</summary>
    /// <param name="hit">打中幾個</param>
    /// <param name="all">本來有幾個</param>
    private static double Share(int hit, int all) =>
        hit / (double)Math.Max(all, 1);

    /// <summary>這一列有沒有被關鍵字打中</summary>
    /// <param name="one">那一列</param>
    /// <param name="word">關鍵字</param>
    private static bool Matches(PaletteItem one, string word) =>
        word.Length == 0 || one.Label.Contains(word) || one.Hint.Contains(word);

    /// <summary>把命中的那幾個字切出來，畫面才標得了色</summary>
    /// <param name="one">那一列</param>
    /// <param name="word">關鍵字</param>
    private static PaletteItem Lit(PaletteItem one, string word)
    {
        var at = new[] { word }.Where(two => two.Length > 0).Select(two => one.Label.IndexOf(two)).DefaultIfEmpty(-1).First();
        var found = new[] { at }.Where(two => two >= 0).ToList();

        return new PaletteItem
        {
            Paths = one.Paths,
            Label = one.Label,
            Hint = one.Hint,
            Tag = one.Tag,
            Tags = new[] { one.Tag }.Where(two => two.Length > 0).ToList(),
            Remembers = one.Remembers,
            Pre = found.Select(two => one.Label[..two]).DefaultIfEmpty(one.Label).First(),
            Hit = found.Select(two => one.Label.Substring(two, word.Length)).DefaultIfEmpty(string.Empty).First(),
            Post = found.Select(two => one.Label[(two + word.Length)..]).DefaultIfEmpty(string.Empty).First(),
            Opens = one.Opens,
            Catalogs = one.Catalogs,
            Settings = one.Settings,
        };
    }

    #endregion

    #region 對照

    #endregion

    #region 模型

    /// <summary>指令面板這一頁</summary>
    public class PaletteView
    {
        /// <summary>搜尋框現在的字</summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>搜尋框沒有字時顯示的提示</summary>
        public string Hint { get; set; } = string.Empty;

        /// <summary>右邊那一格鍵盤提示</summary>
        public string EscLabel { get; set; } = string.Empty;

        /// <summary>放大鏡的每一段路徑</summary>
        public List<string> SearchPaths { get; set; } = [];

        /// <summary>分好組的每一列</summary>
        public List<GroupItem> Groups { get; set; } = [];

        /// <summary>一列都沒有時的那一段；有列就是零筆</summary>
        public List<EmptyView> Empties { get; set; } = [];
    }

    /// <summary>一組</summary>
    public class GroupItem
    {
        /// <summary>這一組叫什麼</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>這一組被這次搜尋打中的比例，愈高排愈前面</summary>
        public double Rank { get; set; }

        /// <summary>這一組底下的每一列</summary>
        public List<PaletteItem> Items { get; set; } = [];
    }

    /// <summary>面板上的一列</summary>
    public class PaletteItem
    {
        /// <summary>圖示的每一段路徑</summary>
        public List<string> Paths { get; set; } = [];

        /// <summary>整行的字</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>命中前面那一段</summary>
        public string Pre { get; set; } = string.Empty;

        /// <summary>命中的那幾個字</summary>
        public string Hit { get; set; } = string.Empty;

        /// <summary>命中後面那一段</summary>
        public string Post { get; set; } = string.Empty;

        /// <summary>第二行的說明</summary>
        public string Hint { get; set; } = string.Empty;

        /// <summary>右邊的小標</summary>
        public string Tag { get; set; } = string.Empty;

        /// <summary>有小標才畫；沒有就是零筆</summary>
        public List<string> Tags { get; set; } = [];

        /// <summary>選過之後要記起來的那幾個字；不是工具就是零筆</summary>
        public List<string> Remembers { get; set; } = [];

        /// <summary>按下去要打開的工具代碼；不是工具就是零筆</summary>
        public List<string> Opens { get; set; } = [];

        /// <summary>按下去回工具目錄</summary>
        public List<bool> Catalogs { get; set; } = [];

        /// <summary>按下去開設定</summary>
        public List<bool> Settings { get; set; } = [];

    }

    /// <summary>一列都沒有時顯示的那一段</summary>
    public class EmptyView
    {
        /// <summary>標題</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>說明</summary>
        public string Text { get; set; } = string.Empty;
    }

    #endregion
}
