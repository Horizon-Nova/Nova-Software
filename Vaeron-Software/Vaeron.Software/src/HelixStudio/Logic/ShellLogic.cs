using System.Diagnostics;
using Models.HelixStudio;

namespace HelixStudio.Logic;

/// <summary>外殼的純運算</summary>
/// <remarks>不碰資料庫也不碰畫面，給什麼算什麼；沒有狀態，所以是靜態的。</remarks>
public static class ShellLogic
{
    #region 開啟平台

    /// <summary>開啟平台：側邊導覽列放哪幾支工具、狀態列每一格的值</summary>
    /// <param name="rows">vw_shell：shell 那一列（工具總數、最後匯入）與每一支工具</param>
    /// <param name="icons">圖示名稱對到的每一段路徑</param>
    public static ShellView Shell(IReadOnlyList<vw_shell> rows, Dictionary<string, List<string>> icons) =>
        new()
        {
            Product = "HelixStudio",
            Crumb = "— 工具目錄",
            SearchHint = "搜尋工具與動作",
            SearchLabel = "搜尋工具、維護團隊或動作",
            Icons = icons,

            // 側邊導覽列：第一列固定是工具目錄，接著是裝好的每一支工具
            Rail = Enumerable.Repeat(new RailItem { Key = string.Empty, Name = "工具目錄", Paths = icons["layout-grid"] }, 1)
                .Concat(rows.Where(row => row.type == "tool").Select(row => new RailItem { Key = $"{row.tool_no}", Name = $"{row.name}", Paths = icons[$"{row.icon}"] }))
                .ToList(),

            Summary = rows.FirstOrDefault(row => row.type == "shell") ?? new(),

            // 這台電腦自己的名字：讀這台電腦，不寫死
            Host = Environment.MachineName,

            // 用戶端版本：執行中的這支程式自己的檔案版本
            Client = $"{FileVersionInfo.GetVersionInfo(Environment.ProcessPath!).FileVersion}",
        };

    #endregion

    #region 跳出一則通知

    /// <summary>跳出一則通知：說什麼、左邊那條色條什麼顏色、停多久</summary>
    /// <param name="text">要說的話</param>
    /// <param name="kind">哪一種：info 或 warn</param>
    /// <param name="icons">圖示名稱對到的每一段路徑</param>
    public static NoteItem Note(string text, string kind, Dictionary<string, List<string>> icons) =>
        new()
        {
            Text = text,
            Paths = icons[NoteIcon[kind]],
            Ink = NoteInk[kind],

            Seconds = NoteSeconds[kind],
        };

    #endregion

    #region 出了狀況

    /// <summary>出了狀況：說發生什麼事、附上原文、給一條出路</summary>
    /// <param name="detail">系統回報的原文</param>
    /// <param name="icons">圖示名稱對到的每一段路徑</param>
    public static FaultItem Fault(string detail, Dictionary<string, List<string>> icons) =>
        new()
        {
            Text = "讀不到資料，畫面上的內容可能不是最新的。",
            Detail = detail,
            RetryLabel = "重試",
            Paths = icons["triangle-alert"],
            Ink = Ink["Danger"],
            Back = Ink["DangerBack"],
            Line = Ink["DangerLine"],
        };

    #endregion

    #region 對照

    /// <summary>啟動時讀一次的那一份設定；要改設定就重啟</summary>
    public static SettingsLogic.SetupView Setup { get; private set; } = new();

    /// <summary>把這一份設定定下來</summary>
    /// <param name="setup">啟動時讀出來的設定</param>
    public static void Adopt(SettingsLogic.SetupView setup) =>
        Setup = setup;

    /// <summary>這一套顏色：畫面與運算都從這裡取</summary>
    public static IReadOnlyDictionary<string, string> Ink { get; } = new Dictionary<string, string>
    {
        ["WindowBack"] = "#f3f3f3",
        ["ContentBack"] = "#fafafa",
        ["Surface"] = "#ffffff",
        ["TextPrimary"] = "#1b1b1b",
        ["TextSecondary"] = "#3b3b3b",
        ["TextMuted"] = "#5c5c5c",
        ["TextFaint"] = "#767676",
        ["TextGhost"] = "#a0a0a0",
        ["HairLine"] = "#14000000",
        ["CheckLine"] = "#66000000",
        ["Divider"] = "#1a000000",
        ["HoverBack"] = "#0f000000",
        ["PressBack"] = "#1a000000",
        ["Track"] = "#ebebeb",
        ["Danger"] = "#a4262c",
        ["DangerBack"] = "#fdf3f2",
        ["DangerLine"] = "#66a4262c",
        ["CloseHover"] = "#c42b1c",
        ["Badge"] = "#c42b1c",
        ["Scrim"] = "#40000000",
        ["ShineDim"] = "#ededed",
        ["ShineLit"] = "#f6f6f6",
        ["OnAccent"] = "#ffffff",
        ["Accent"] = "#0f6cbd",
        ["AccentHover"] = "#0057a6",
    };

    /// <summary>主色由淺到深九階：以 #0f6cbd 的 OKLCH（L 0.526、C 0.149、H -108.4°）為基準，亮度等距、兩端收斂彩度</summary>
    public static IReadOnlyDictionary<int, string> Brand { get; } = new Dictionary<int, string>
    {
        [100] = "#e4f8ff",
        [200] = "#c5e8ff",
        [300] = "#98cfff",
        [400] = "#61a9f6",
        [500] = "#0f6cbd",
        [600] = "#0057a6",
        [700] = "#004086",
        [800] = "#002861",
        [900] = "#00133b",
    };

    /// <summary>每一種通知的圖示</summary>
    public static IReadOnlyDictionary<string, string> NoteIcon { get; } = new Dictionary<string, string>
    {
        ["info"] = "check",
        ["warn"] = "triangle-alert",
    };

    /// <summary>每一種通知左邊色條的顏色</summary>
    public static IReadOnlyDictionary<string, string> NoteInk => new Dictionary<string, string>
    {
        ["info"] = Ink["Accent"],
        ["warn"] = Ink["Danger"],
    };

    /// <summary>每一種通知停幾秒：資訊 4、警告 7</summary>
    public static IReadOnlyDictionary<string, int> NoteSeconds { get; } = new Dictionary<string, int>
    {
        ["info"] = 4,
        ["warn"] = 7,
    };

    #endregion

    #region 模型

    /// <summary>外殼畫面上的每一格</summary>
    public class ShellView
    {
        /// <summary>標題列的產品名</summary>
        public string Product { get; set; } = string.Empty;

        /// <summary>標題列的麵包屑</summary>
        public string Crumb { get; set; } = string.Empty;

        /// <summary>搜尋藥丸的滑鼠提示</summary>
        public string SearchHint { get; set; } = string.Empty;

        /// <summary>搜尋藥丸上的字</summary>
        public string SearchLabel { get; set; } = string.Empty;

        /// <summary>圖示名稱對到的每一段路徑</summary>
        public Dictionary<string, List<string>> Icons { get; set; } = [];

        /// <summary>側邊導覽列上的每一支工具</summary>
        public List<RailItem> Rail { get; set; } = [];

        /// <summary>vw_shell 的 shell 那一列：狀態列的數字</summary>
        public vw_shell Summary { get; set; } = new();

        /// <summary>這台電腦的名字</summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>用戶端版本</summary>
        public string Client { get; set; } = string.Empty;

    }

    /// <summary>跳出來的一則通知</summary>
    public class NoteItem
    {
        /// <summary>說了什麼</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>圖示的每一段路徑</summary>
        public List<string> Paths { get; set; } = [];

        /// <summary>左邊色條與圖示的顏色</summary>
        public string Ink { get; set; } = string.Empty;

        /// <summary>停幾秒</summary>
        public int Seconds { get; set; }

        /// <summary>滑鼠停在上面時計時跟著停</summary>
        public bool Held { get; set; }
    }

    /// <summary>出了狀況時橫幅上的那一段</summary>
    public class FaultItem
    {
        /// <summary>發生了什麼事</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>系統回報的原文</summary>
        public string Detail { get; set; } = string.Empty;

        /// <summary>出路那顆按鈕上的字</summary>
        public string RetryLabel { get; set; } = string.Empty;

        /// <summary>圖示的每一段路徑</summary>
        public List<string> Paths { get; set; } = [];

        /// <summary>文字與圖示的顏色</summary>
        public string Ink { get; set; } = string.Empty;

        /// <summary>底色</summary>
        public string Back { get; set; } = string.Empty;

        /// <summary>框線色</summary>
        public string Line { get; set; } = string.Empty;
    }

    /// <summary>側邊導覽列上的一顆按鈕</summary>
    public class RailItem
    {
        /// <summary>工具代碼</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>工具名稱</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>圖示的每一段路徑</summary>
        public List<string> Paths { get; set; } = [];

        /// <summary>這一格現在長什麼樣；重讀時拿它比，一樣就不動那一格</summary>
        public string Mark => $"{Key}|{Name}";
    }

    #endregion
}
