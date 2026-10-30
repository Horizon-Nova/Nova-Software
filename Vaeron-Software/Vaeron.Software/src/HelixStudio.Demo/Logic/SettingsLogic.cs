using Models.HelixStudio;

namespace HelixStudio.Logic;

/// <summary>設定這一頁的純運算（Demo 版：工具設定不上鎖）</summary>
/// <remarks>
/// 不碰資料庫也不碰畫面，給什麼算什麼；沒有狀態，所以是靜態的。
/// 區段、標籤、說明、選項的字是設計圖上固定的字，寫在設定頁的 XAML；這裡只把 vw_settings 的值攤成畫面要的形狀，與算出要寫回去的那幾列。
/// </remarks>
public static class SettingsLogic
{
    #region 打開設定

    /// <summary>打開設定：每一項現在的值、左邊「工具」底下的每一支、使用紀錄表</summary>
    /// <param name="rows">vw_settings 查到的每一列</param>
    /// <param name="icons">圖示名稱對到的每一段路徑</param>
    /// <param name="section">現在看的是哪一節；看工具時是 tools:工具代碼</param>
    public static SettingsView Settings(IReadOnlyList<vw_setting> rows, Dictionary<string, List<string>> icons, string section)
    {
        // vw_settings 的兩種列：每一項設定、每一支工具（有設定畫面的掛在「工具」底下；全部都進使用紀錄表）
        var keys = rows.Where(row => row.type == "tool").ToList();
        var tools = keys.Where(row => $"{row.settings}".Length > 0).ToList();

        // 現在看的那一支工具；看的不是工具就是零筆
        var here = tools.Where(tool => $"{Tools}:{tool.tool_no}" == section).ToList();

        return new SettingsView
        {
            // 每一項現在的值，照設定代碼取；跟資訊部建議的值不一樣就是改過
            Values = rows
                .Where(row => row.type == "setting")
                .ToDictionary(row => $"{row.setting_key}", row => new ValueItem
                {
                    Key = $"{row.setting_key}",
                    On = row.enabled == true,
                    Number = row.number,
                    Path = $"{row.path}",
                    Paths = new[] { $"{row.path}" }.Where(one => one.Length > 0).ToList(),
                    Moved = row.moved == true,
                }),

            // 左邊「工具」底下：有設定畫面的每一支
            Tools = tools.Select(tool => new NavItem
            {
                Key = $"{Tools}:{tool.tool_no}",
                Text = $"{tool.name}",
                Chosen = $"{Tools}:{tool.tool_no}" == section,
            }).ToList(),

            // 關於本軟體：每支工具的使用紀錄
            Ledgers = keys,

            Section = section,
            Here = here.Count > 0,

            ClosePaths = icons["x"],
            SearchPaths = icons["search"],
            OpenPaths = icons["chevron-down"],
            ShutPaths = icons["chevron-right"],
        };
    }

    #endregion

    #region 啟動時套用

    /// <summary>啟動時與改過設定時，把設定讀成程式用得到的值</summary>
    /// <param name="rows">vw_settings 查到的每一列</param>
    public static SetupView Applied(IReadOnlyList<vw_setting> rows)
    {
        // 一項設定現在的值
        var value = rows.Where(row => row.type == "setting").ToDictionary(row => $"{row.setting_key}");

        return new SetupView
        {
            // 文字大小：存的就是百分比
            Scale = (double)(value.GetValueOrDefault("textScale")?.number ?? Percent) / Percent,

            // 大按鈕模式：按鈕與輸入框加高到 44（設計寫「加高到 44px 以上」）；關著時不墊高，照字的行高推
            Height = value.GetValueOrDefault("bigTargets")?.enabled == true ? Tall : 0,

            Contrast = value.GetValueOrDefault("highContrast")?.enabled == true,
            Awake = value.GetValueOrDefault("screenAwake")?.enabled == true,
            Boot = value.GetValueOrDefault("launchOnBoot")?.enabled == true,
            Sound = value.GetValueOrDefault("notifySound")?.enabled == true,
        };
    }

    #endregion

    #region 基本 CRUD 操作

    /// <summary>切一個開關、選一個選項：那一項現在的值整列帶過來，換成新的值</summary>
    /// <param name="rows">vw_settings 查到的每一列</param>
    /// <param name="key">哪一項</param>
    /// <param name="on">開關切成什麼；不是開關是 null</param>
    /// <param name="number">選了哪一個數字；不是數字是 null</param>
    /// <param name="now">這一次的時刻</param>
    public static List<setting> Changed(IReadOnlyList<vw_setting> rows, string key, bool? on, int? number, DateTime now) =>
        [.. rows
            .Where(row => row.type == "setting" && row.setting_key == key)
            .Select(row => new setting
            {
                id = row.id ?? 0,
                type = "value",
                setting_key = key,
                enabled = on ?? row.enabled,
                number = number ?? row.number,
                path = row.path,
                updated_at = now,
            })];

    /// <summary>回復預設：那幾項現在的值換成資訊部建議的值；給空的清單就是每一項</summary>
    /// <param name="rows">vw_settings 查到的每一列</param>
    /// <param name="keys">哪幾項；空的就是每一項</param>
    /// <param name="now">這一次的時刻</param>
    public static List<setting> Reverted(IReadOnlyList<vw_setting> rows, IReadOnlyList<string> keys, DateTime now) =>
        [.. rows
            .Where(row => row.type == "setting" && (keys.Count == 0 || keys.Contains($"{row.setting_key}")))
            .Select(row => new setting
            {
                id = row.id ?? 0,
                type = "value",
                setting_key = $"{row.setting_key}",
                enabled = row.fallback_enabled,
                number = row.fallback_number,
                path = row.fallback_path,
                updated_at = now,
            })];

    #endregion

    #region 對照

    /// <summary>「工具」那一個分類的代碼；底下每一節是「tools:工具代碼」</summary>
    public const string Tools = "tools";

    /// <summary>倍率是以百分比寫的</summary>
    private const int Percent = 100;

    /// <summary>大按鈕模式開著時控制項至少多高</summary>
    private const double Tall = 44;

    #endregion

    #region 模型

    /// <summary>啟動時讀出來的那一份設定</summary>
    public class SetupView
    {
        /// <summary>文字放大幾倍</summary>
        public double Scale { get; set; } = 1;

        /// <summary>按鈕與輸入框至少多高；0 是不墊高</summary>
        public double Height { get; set; }

        /// <summary>要不要高對比</summary>
        public bool Contrast { get; set; }

        /// <summary>使用中保持螢幕不休眠</summary>
        public bool Awake { get; set; }

        /// <summary>開機時自動啟動</summary>
        public bool Boot { get; set; }

        /// <summary>通知音效</summary>
        public bool Sound { get; set; }
    }

    /// <summary>設定這一頁</summary>
    public class SettingsView
    {
        /// <summary>每一項現在的值，照設定代碼取</summary>
        public Dictionary<string, ValueItem> Values { get; set; } = [];

        /// <summary>左邊「工具」底下有設定畫面的每一支</summary>
        public List<NavItem> Tools { get; set; } = [];

        /// <summary>關於本軟體：每支工具的使用紀錄</summary>
        public List<vw_setting> Ledgers { get; set; } = [];

        /// <summary>選中的那一節的代碼</summary>
        public string Section { get; set; } = string.Empty;

        /// <summary>看的是一支工具的設定</summary>
        public bool Here { get; set; }

        /// <summary>關閉、清除的叉叉</summary>
        public List<string> ClosePaths { get; set; } = [];

        /// <summary>放大鏡</summary>
        public List<string> SearchPaths { get; set; } = [];

        /// <summary>分類展開時的箭頭</summary>
        public List<string> OpenPaths { get; set; } = [];

        /// <summary>分類收起時的箭頭</summary>
        public List<string> ShutPaths { get; set; } = [];
    }

    /// <summary>一項設定現在的值</summary>
    public class ValueItem
    {
        /// <summary>設定代碼</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>開關類：開著嗎</summary>
        public bool On { get; set; }

        /// <summary>數字類：選的那一個數字</summary>
        public int? Number { get; set; }

        /// <summary>路徑類：現在的路徑</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>路徑類的選單：只有現在那一個；還沒有值是零筆</summary>
        public List<string> Paths { get; set; } = [];

        /// <summary>跟資訊部建議的值不一樣</summary>
        public bool Moved { get; set; }
    }

    /// <summary>左邊「工具」底下的一支</summary>
    public class NavItem
    {
        /// <summary>這一節的代碼：tools:工具代碼</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>工具名稱</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>是不是現在看的</summary>
        public bool Chosen { get; set; }
    }

    #endregion
}
