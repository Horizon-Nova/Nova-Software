using System.Text.Json;
using Models.HelixStudio;

namespace HelixStudio.Logic;

/// <summary>設定這一頁的純運算</summary>
/// <remarks>不碰資料庫也不碰畫面，給什麼算什麼；沒有狀態，所以是靜態的。</remarks>
public static class SettingsLogic
{
    #region 打開設定

    /// <summary>打開設定：左邊的分類樹、右邊那一節的每一列</summary>
    /// <param name="rows">vw_setting 查到的每一列</param>
    /// <param name="icons">圖示名稱對到的每一段路徑</param>
    /// <param name="section">現在看的是哪一節</param>
    /// <param name="opened">左邊哪幾個分類是展開的</param>
    /// <param name="query">搜尋設定打的字</param>
    /// <param name="until">輸入工具金鑰解開的工具，各自到什麼時刻鎖回</param>
    /// <param name="now">這一刻</param>
    public static SettingsView Settings(IReadOnlyList<vw_setting> rows, Dictionary<string, List<string>> icons,
        string section, IReadOnlyList<string> opened, string query,
        IReadOnlyDictionary<string, DateTime> until, DateTime now)
    {
        // vw_settings 的兩種列：每一項設定、每一支工具（有設定畫面的掛在「工具」底下；全部都進使用紀錄表）
        var keys = rows.Where(row => row.type == "tool").ToList();
        var tools = keys.Where(row => $"{row.settings}".Length > 0).ToList();

        // 每支工具的設定還剩多久鎖回；鎖著的是零
        var left = tools.ToDictionary(tool => $"{tool.tool_no}",
            tool => new[] { until.GetValueOrDefault($"{tool.tool_no}") - now }.Where(one => one > TimeSpan.Zero).DefaultIfEmpty(TimeSpan.Zero).First());

        // 現在看的那一支工具；看的不是工具就是零筆
        var here = tools.Where(tool => $"{Tools}:{tool.tool_no}" == section).ToList();

        // 名稱對得上搜尋的工具
        var tooled = tools.Where(tool => $"{tool.name}".Contains(query)).ToList();

        // 關鍵字比標題、說明與旁邊那一句
        var matched = rows
            .Where(row => row.type == "setting")
            .Where(row => new[] { row.label, row.note, row.inline, row.section_label }.Any(one => $"{one}".Contains(query)))
            .ToList();

        // 選中的那一節：看工具就是那支工具；不是工具又不在這次搜到的節裡，就落在第一節
        var current = section.StartsWith($"{Tools}:") || matched.Any(row => $"{row.section_key}" == section)
            ? section
            : matched.OrderBy(row => row.sort).Select(row => $"{row.section_key}").FirstOrDefault() ?? string.Empty;

        // 左邊的樹：分類一列，展開的分類底下接它的每一節
        var nav = matched
            .Select(row => new { Key = $"{row.area_key}", Label = $"{row.area_label}" })
            .Distinct()
            .SelectMany(area => Enumerable
                .Repeat(new NavItem
                {
                    Key = area.Key,
                    Text = area.Label,
                    Parent = true,
                    Paths = icons[Twist[opened.Contains(area.Key)]],
                }, 1)
                .Concat(matched
                    .Where(row => $"{row.area_key}" == area.Key)
                    .Where(_ => opened.Contains(area.Key))
                    .Select(row => new { Key = $"{row.section_key}", Label = $"{row.section_label}" })
                    .Distinct()
                    .Select(one => new NavItem
                    {
                        Key = one.Key,
                        Text = one.Label,
                        Parent = false,
                        Chosen = one.Key == current,
                        Back = Chosen[one.Key == current],
                        Ink = ChosenInk[one.Key == current],
                        Bar = Bar[one.Key == current],
                    })))
            .Concat(tooled.Take(1).Select(_ => new NavItem
            {
                Key = Tools,
                Text = "工具",
                Parent = true,
                Paths = icons[Twist[opened.Contains(Tools)]],
            }))
            .Concat(tooled.Where(_ => opened.Contains(Tools)).Select(tool => new NavItem
            {
                Key = $"{Tools}:{tool.tool_no}",
                Text = $"{tool.name}",
                Parent = false,
                Chosen = $"{Tools}:{tool.tool_no}" == section,
                Back = Chosen[$"{Tools}:{tool.tool_no}" == section],
                Ink = ChosenInk[$"{Tools}:{tool.tool_no}" == section],
                Bar = Bar[$"{Tools}:{tool.tool_no}" == section],
                LockPaths = [icons[Lock[left[$"{tool.tool_no}"] > TimeSpan.Zero]]],
                Left = $"{left[$"{tool.tool_no}"]:m\\:ss}",
                Open = left[$"{tool.tool_no}"] > TimeSpan.Zero,
            }))
            .ToList();

        // 右邊：不是看工具時，每一節照順序排在同一頁，一節一段；按鈕那一種列放進這一節最後的危險區
        var sections = matched
            .Where(_ => here.Count == 0)
            .GroupBy(row => $"{row.section_key}")
            .OrderBy(one => one.Min(row => row.sort))
            .Select(one => new SectionItem
            {
                Key = one.Key,
                Area = $"{one.First().area_label}",
                Title = $"{one.First().section_label}",
                Note = $"{one.First().section_note}".Length > 0 ? $"{one.First().section_note}" : null,
                Rows = one.Where(row => row.kind != "action").OrderBy(row => row.sort).Select(Row).ToList(),
                Dangers = one.Where(row => row.kind == "action").OrderBy(row => row.sort).Select(Row).ToList(),
                Ledgers = keys.Take(1).Where(_ => one.Key == About).Select(_ => keys.ToList()).ToList(),
            })
            .ToList();

        return new SettingsView
        {
            Title = "設定",
            Hint = "搜尋設定",
            Query = query,
            ClosePaths = icons["x"],
            SearchPaths = icons["search"],
            CheckPaths = icons["check"],
            DownPaths = icons["chevron-down"],
            Count = $"{matched.Count} 項設定",
            Clears = new[] { query }.Where(one => one.Length > 0).ToList(),
            Nav = nav,
            Section = current,
            Sections = sections,

            // 工具的那一節：鎖著就是鎖卡，解開就是倒數那一條
            Locks = here.Where(tool => left[$"{tool.tool_no}"] == TimeSpan.Zero).Select(tool => new LockItem
            {
                Name = $"{tool.name}",
                Minutes = tool.unlock_minutes ?? 0,
                Paths = icons["lock"],
            }).ToList(),
            Opens = here.Where(tool => left[$"{tool.tool_no}"] > TimeSpan.Zero).Select(tool => new OpenItem
            {
                Left = $"{left[$"{tool.tool_no}"]:m\\:ss}",
                Paths = icons["lock-open"],
                LockPaths = icons["lock"],
            }).ToList(),
        };
    }

    /// <summary>設定裡的一列：標題、說明，還有它那一種控制項</summary>
    /// <param name="row">這一項那一列</param>
    private static RowItem Row(vw_setting row)
    {
        // 現在的值與資訊部建議的值不一樣，才顯示圓點與重設
        var moved = new[] { $"{row.value}" }.Where(one => one != $"{row.fallback}").ToList();

        return new RowItem
        {
            Key = $"{row.setting_key}",
            Label = $"{row.label}",
            Notes = new[] { $"{row.note}" }.Where(one => one.Length > 0).ToList(),
            Tip = $"{row.note}".Length > 0 ? $"{row.note}" : null,
            Moveds = moved.Select(_ => new ResetItem
            {
                Key = $"{row.setting_key}",
                Fallback = $"{row.fallback}",
                Label = "回復預設",
            }).ToList(),
            Bar = Moved[moved.Count > 0],
            Ids = new[] { $"{row.setting_key}" }.Where(one => one.Length > 0).Select(one => $"shell.{one}").ToList(),
            ResetLabel = "回復預設",
            Value = $"{row.value}",
            Fallback = $"{row.fallback}",

            // 這一列是哪一種控制項，就只有那一種是一筆，其餘都是零筆
            Toggles = new[] { $"{row.kind}" }.Where(one => one == "toggle").Select(_ => new ToggleItem
            {
                Key = $"{row.setting_key}",
                On = $"{row.value}" == "true",
                Inline = $"{row.inline}",
                Next = Flip[$"{row.value}"],
            }).ToList(),
            Choices = new[] { $"{row.kind}" }.Where(one => one == "choice").Select(_ => new ChoiceItem
            {
                Key = $"{row.setting_key}",
                Options = JsonSerializer.Deserialize<List<string>>($"{row.options}") ?? [],
                Value = $"{row.value}",
            }).ToList(),
            Infos = new[] { $"{row.kind}" }.Where(one => one == "info").Select(_ => $"{row.value}").ToList(),
            Actions = new[] { $"{row.kind}" }.Where(one => one == "action").Select(_ => new ActionItem
            {
                Label = $"{row.button}",
                Ink = Risky[row.danger == true],
                Line = RiskyLine[row.danger == true],
                Said = $"「{row.label}」已送出",
            }).ToList(),
        };
    }

    /// <summary>啟動時把設定讀成程式用得到的值</summary>
    /// <param name="rows">vw_setting 查到的每一列</param>
    public static SetupView Applied(IReadOnlyList<vw_setting> rows)
    {
        // 一項設定現在的值
        var value = rows
            .Where(row => $"{row.setting_key}".Length > 0)
            .GroupBy(row => $"{row.setting_key}")
            .ToDictionary(one => one.Key, one => $"{one.First().value}");

        return new SetupView
        {
            // 文字大小：倍率就寫在那個選項的字面上，取出數字再除以一百
            Scale = Number(value.GetValueOrDefault("textScale", string.Empty)) / Percent,

            // 大按鈕模式：說明那一句寫的就是要加高到多少
            Height = Tall[value.GetValueOrDefault("bigTargets", "false")],

            Contrast = value.GetValueOrDefault("highContrast", "false") == "true",
            Awake = value.GetValueOrDefault("screenAwake", "false") == "true",
            Boot = value.GetValueOrDefault("launchOnBoot", "false") == "true",
            Sound = value.GetValueOrDefault("notifySound", "false") == "true",
            Source = value.GetValueOrDefault("source", string.Empty),
        };
    }

    /// <summary>一句話裡的數字；沒有數字就是一百</summary>
    /// <param name="text">那一句話</param>
    private static double Number(string text) =>
        new[] { string.Concat(text.Where(char.IsDigit)) }
            .Where(one => one.Length > 0)
            .Select(double.Parse)
            .DefaultIfEmpty(Percent)
            .First();

    #endregion

    #region 對照

    /// <summary>倍率是以百分比寫的</summary>
    private const double Percent = 100;

    /// <summary>大按鈕模式開著時控制項多高；設計寫「加高到 44px 以上」</summary>
    private static IReadOnlyDictionary<string, double> Tall { get; } = new Dictionary<string, double>
    {
        ["true"] = 44,
        ["false"] = 32,
    };

    /// <summary>分類展開與收起各用哪一個箭頭</summary>
    /// <summary>「工具」那一個分類的代碼；底下每一節是「tools:工具代碼」</summary>
    public const string Tools = "tools";

    /// <summary>關於本軟體那一節的代碼</summary>
    private const string About = "about";

    /// <summary>工具設定解開時、鎖著時各用哪一個鎖頭</summary>
    private static IReadOnlyDictionary<bool, string> Lock { get; } = new Dictionary<bool, string>
    {
        [true] = "lock-open",
        [false] = "lock",
    };

    private static IReadOnlyDictionary<bool, string> Twist { get; } = new Dictionary<bool, string>
    {
        [true] = "chevron-down",
        [false] = "chevron-right",
    };

    /// <summary>選中的那一節的底色</summary>
    private static IReadOnlyDictionary<bool, string> Chosen => new Dictionary<bool, string>
    {
        [true] = ShellLogic.Brand[100],
        [false] = "#00ffffff",
    };

    /// <summary>選中的那一節的字色</summary>
    private static IReadOnlyDictionary<bool, string> ChosenInk => new Dictionary<bool, string>
    {
        [true] = ShellLogic.Brand[700],
        [false] = ShellLogic.Ink["TextPrimary"],
    };

    /// <summary>選中的那一節左邊那一條</summary>
    private static IReadOnlyDictionary<bool, string> Bar => new Dictionary<bool, string>
    {
        [true] = ShellLogic.Ink["Accent"],
        [false] = "#00ffffff",
    };

    /// <summary>開關按一下之後變成什麼</summary>
    private static IReadOnlyDictionary<string, string> Flip { get; } = new Dictionary<string, string>
    {
        ["true"] = "false",
        ["false"] = "true",
    };

    /// <summary>改過的那一列左邊那一條</summary>
    private static IReadOnlyDictionary<bool, string> Moved => new Dictionary<bool, string>
    {
        [true] = ShellLogic.Ink["Accent"],
        [false] = "#00ffffff",
    };

    /// <summary>會造成損失的那顆按鈕用什麼字色</summary>
    private static IReadOnlyDictionary<bool, string> Risky => new Dictionary<bool, string>
    {
        [true] = ShellLogic.Ink["Danger"],
        [false] = ShellLogic.Ink["TextPrimary"],
    };

    /// <summary>會造成損失的那顆按鈕用什麼框線</summary>
    private static IReadOnlyDictionary<bool, string> RiskyLine => new Dictionary<bool, string>
    {
        [true] = ShellLogic.Ink["DangerLine"],
        [false] = ShellLogic.Ink["HairLine"],
    };

    #endregion

    #region 模型

    /// <summary>啟動時讀出來的那一份設定</summary>
    public class SetupView
    {
        /// <summary>文字放大幾倍</summary>
        public double Scale { get; set; } = 1;

        /// <summary>按鈕與輸入框多高</summary>
        public double Height { get; set; } = 32;

        /// <summary>要不要高對比</summary>
        public bool Contrast { get; set; }

        /// <summary>使用中保持螢幕不休眠</summary>
        public bool Awake { get; set; }

        /// <summary>開機時自動啟動</summary>
        public bool Boot { get; set; }

        /// <summary>通知音效</summary>
        public bool Sound { get; set; }

        /// <summary>套件下載來源</summary>
        public string Source { get; set; } = string.Empty;
    }

    /// <summary>設定這一頁</summary>
    public class SettingsView
    {
        /// <summary>標題</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>搜尋框沒有字時顯示的提示</summary>
        public string Hint { get; set; } = string.Empty;

        /// <summary>搜尋框現在的字</summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>關閉按鈕的每一段路徑</summary>
        public List<string> ClosePaths { get; set; } = [];

        /// <summary>放大鏡的每一段路徑</summary>
        public List<string> SearchPaths { get; set; } = [];

        /// <summary>打勾的每一段路徑</summary>
        public List<string> CheckPaths { get; set; } = [];

        /// <summary>下拉箭頭的每一段路徑</summary>
        public List<string> DownPaths { get; set; } = [];

        /// <summary>符合的有幾項</summary>
        public string Count { get; set; } = string.Empty;

        /// <summary>清除搜尋那顆按鈕；沒打字就是零筆</summary>
        public List<string> Clears { get; set; } = [];

        /// <summary>左邊的分類與節</summary>
        public List<NavItem> Nav { get; set; } = [];

        /// <summary>選中的那一節的代碼</summary>
        public string Section { get; set; } = string.Empty;

        /// <summary>不是看工具時，每一節照順序排在同一頁</summary>
        public List<SectionItem> Sections { get; set; } = [];

        /// <summary>看的那支工具設定鎖著時的鎖卡；其餘是零筆</summary>
        public List<LockItem> Locks { get; set; } = [];

        /// <summary>看的那支工具設定解開時的倒數那一條；其餘是零筆</summary>
        public List<OpenItem> Opens { get; set; } = [];

    }

    /// <summary>工具設定鎖著時那一張卡</summary>
    public class LockItem
    {
        /// <summary>工具名稱</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>輸入工具金鑰後解開幾分鐘：vw_tool_setting.unlock_minutes</summary>
        public int Minutes { get; set; }

        /// <summary>鎖頭的每一段路徑</summary>
        public List<string> Paths { get; set; } = [];
    }

    /// <summary>工具設定解開時上面那一條</summary>
    public class OpenItem
    {
        /// <summary>還剩多久鎖回，分:秒</summary>
        public string Left { get; set; } = string.Empty;

        /// <summary>打開的鎖頭的每一段路徑</summary>
        public List<string> Paths { get; set; } = [];

        /// <summary>鎖回那顆按鈕上鎖頭的每一段路徑</summary>
        public List<string> LockPaths { get; set; } = [];
    }

    /// <summary>左邊樹上的一列</summary>
    public class NavItem
    {
        /// <summary>分類或節的代碼</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>這一列寫什麼</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>這一列是分類還是節</summary>
        public bool Parent { get; set; }

        /// <summary>這一節是不是現在看的</summary>
        public bool Chosen { get; set; }

        /// <summary>箭頭的每一段路徑</summary>
        public List<string> Paths { get; set; } = [];

        /// <summary>底色</summary>
        public string Back { get; set; } = string.Empty;

        /// <summary>字色</summary>
        public string Ink { get; set; } = string.Empty;

        /// <summary>左邊那一條的顏色</summary>
        public string Bar { get; set; } = string.Empty;

        /// <summary>工具那一節的鎖頭（鎖著或解開）；不是工具就是零筆</summary>
        public List<List<string>> LockPaths { get; set; } = [];

        /// <summary>工具設定還剩多久鎖回，分:秒</summary>
        public string Left { get; set; } = string.Empty;

        /// <summary>工具設定現在是不是解開的</summary>
        public bool Open { get; set; }
    }

    /// <summary>設定頁上的一節</summary>
    public class SectionItem
    {
        /// <summary>這一節的代碼，左邊樹上點它就捲到這裡</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>屬於哪一個分類</summary>
        public string Area { get; set; } = string.Empty;

        /// <summary>這一節叫什麼</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>這一節的說明，放在標題的提示框；沒有是 null</summary>
        public string? Note { get; set; }

        /// <summary>一項設定一列</summary>
        public List<RowItem> Rows { get; set; } = [];

        /// <summary>按下去會造成改變的按鈕，放在這一節最後的危險區；沒有就是零筆</summary>
        public List<RowItem> Dangers { get; set; } = [];

        /// <summary>關於本軟體：每支工具的使用紀錄表，一張；其餘節是零筆</summary>
        public List<List<vw_setting>> Ledgers { get; set; } = [];
    }

    /// <summary>設定裡的一列</summary>
    public class RowItem
    {
        /// <summary>這一項的代碼</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>標題</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>說明；沒有就是零筆</summary>
        public List<string> Notes { get; set; } = [];

        /// <summary>說明，放在名稱的提示框；沒有是 null</summary>
        public string? Tip { get; set; }

        /// <summary>改過的那幾筆；沒改過就是零筆</summary>
        public List<ResetItem> Moveds { get; set; } = [];

        /// <summary>改過的那一列左邊那一條的顏色</summary>
        public string Bar { get; set; } = string.Empty;

        /// <summary>這一項的設定代碼；沒有代碼就是零筆</summary>
        public List<string> Ids { get; set; } = [];

        /// <summary>重設那顆按鈕上的字</summary>
        public string ResetLabel { get; set; } = string.Empty;

        /// <summary>現在的值</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>資訊部建議的值</summary>
        public string Fallback { get; set; } = string.Empty;

        /// <summary>這一列是開關時的那一筆</summary>
        public List<ToggleItem> Toggles { get; set; } = [];

        /// <summary>這一列是選單時的那一筆</summary>
        public List<ChoiceItem> Choices { get; set; } = [];

        /// <summary>這一列只是說明時的那一筆</summary>
        public List<string> Infos { get; set; } = [];

        /// <summary>這一列是按鈕時的那一筆</summary>
        public List<ActionItem> Actions { get; set; } = [];
    }

    /// <summary>回復預設那一顆</summary>
    public class ResetItem
    {
        /// <summary>這一項的代碼</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>資訊部建議的值</summary>
        public string Fallback { get; set; } = string.Empty;

        /// <summary>按鈕上的字</summary>
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>一個開關</summary>
    public class ToggleItem
    {
        /// <summary>這一項的代碼</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>現在是開的嗎</summary>
        public bool On { get; set; }

        /// <summary>開關旁邊那一句</summary>
        public string Inline { get; set; } = string.Empty;

        /// <summary>按一下之後變成什麼</summary>
        public string Next { get; set; } = string.Empty;
    }

    /// <summary>一個選單</summary>
    public class ChoiceItem
    {
        /// <summary>這一項的代碼</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>可以選的每一項</summary>
        public List<string> Options { get; set; } = [];

        /// <summary>現在選的那一項</summary>
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>一顆按鈕</summary>
    public class ActionItem
    {
        /// <summary>按鈕上的字</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>字色</summary>
        public string Ink { get; set; } = string.Empty;

        /// <summary>框線色</summary>
        public string Line { get; set; } = string.Empty;

        /// <summary>按下去之後說的那一句</summary>
        public string Said { get; set; } = string.Empty;
    }

    #endregion
}
