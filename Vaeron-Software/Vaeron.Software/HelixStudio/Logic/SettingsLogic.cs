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
    public static SettingsView Settings(IReadOnlyList<vw_setting> rows, Dictionary<string, List<string>> icons,
        string section, IReadOnlyList<string> opened, string query)
    {
        // 關鍵字比標題、說明與旁邊那一句
        var matched = rows
            .Where(row => new[] { row.label, row.note, row.inline, row.section_label }.Any(one => $"{one}".Contains(query)))
            .ToList();

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
                        Chosen = one.Key == section,
                        Back = Chosen[one.Key == section],
                        Ink = ChosenInk[one.Key == section],
                        Bar = Bar[one.Key == section],
                    })))
            .ToList();

        // 右邊：這一節的每一群，每一群底下的每一列
        var here = matched.Where(row => $"{row.section_key}" == section).ToList();

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
            Notes = here.Select(row => $"{row.section_note}").Where(one => one.Length > 0).Take(1).ToList(),
            Groups = here
                .Select(row => $"{row.group_label}")
                .Distinct()
                .Select(one => new GroupItem
                {
                    Title = one,
                    Rows = here.Where(row => $"{row.group_label}" == one).Select(Row).ToList(),
                })
                .ToList(),
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
            Start = value.GetValueOrDefault("startView", string.Empty),
            AssistOpen = value.GetValueOrDefault("aiOpenOnStart", "false") == "true",
            AssistDock = value.GetValueOrDefault("aiDock", "true") == "true",
            Cite = value.GetValueOrDefault("aiCite", "true") == "true",
            KeepLog = value.GetValueOrDefault("aiKeepLog", "false") == "true",
            Awake = value.GetValueOrDefault("screenAwake", "false") == "true",
            Boot = value.GetValueOrDefault("launchOnBoot", "false") == "true",
            AutoUpdate = value.GetValueOrDefault("updateAuto", "false") == "true",
            Approval = value.GetValueOrDefault("updateNeedApproval", "false") == "true",
            Sound = value.GetValueOrDefault("notifySound", "false") == "true",
            Source = value.GetValueOrDefault("source", string.Empty),
            Ticket = value.GetValueOrDefault("notifyTicket", "false") == "true",
            SignedIn = value.GetValueOrDefault("keepSignedIn", "false") == "true",

            // 允許更新的時段：那個選項的字面上有幾個時刻就是幾個，沒有時刻就表示只在手動時更新
            Window = Times(value.GetValueOrDefault("updateWindow", string.Empty)),
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

    /// <summary>一句話裡寫到的每一個時刻</summary>
    /// <param name="text">那一句話</param>
    private static List<TimeSpan> Times(string text) =>
        text.Split([' ', '（', '）', '/', '–', '-', '，'], StringSplitOptions.RemoveEmptyEntries)
            .Where(one => one.Contains(':'))
            .Select(one => TimeSpan.Parse(one))
            .ToList();

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

        /// <summary>開起來先看哪一個畫面</summary>
        public string Start { get; set; } = string.Empty;

        /// <summary>開起來就展開 AI 助理</summary>
        public bool AssistOpen { get; set; }

        /// <summary>AI 助理停靠在右側面板</summary>
        public bool AssistDock { get; set; }

        /// <summary>回答附上來源文件</summary>
        public bool Cite { get; set; }

        /// <summary>保留問答紀錄</summary>
        public bool KeepLog { get; set; }

        /// <summary>使用中保持螢幕不休眠</summary>
        public bool Awake { get; set; }

        /// <summary>開機時自動啟動</summary>
        public bool Boot { get; set; }

        /// <summary>自動下載並安裝更新</summary>
        public bool AutoUpdate { get; set; }

        /// <summary>允許自動更新的那幾個時刻；沒有時刻就是只在手動時更新</summary>
        public List<TimeSpan> Window { get; set; } = [];

        /// <summary>安裝非必裝工具需主管核可</summary>
        public bool Approval { get; set; }

        /// <summary>通知音效</summary>
        public bool Sound { get; set; }

        /// <summary>套件下載來源</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>工單與遠端支援狀態要不要通知</summary>
        public bool Ticket { get; set; }

        /// <summary>保持登入狀態</summary>
        public bool SignedIn { get; set; }
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

        /// <summary>這一節上面那一句；沒有就是零筆</summary>
        public List<string> Notes { get; set; } = [];

        /// <summary>這一節的每一群</summary>
        public List<GroupItem> Groups { get; set; } = [];
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
    }

    /// <summary>一群設定</summary>
    public class GroupItem
    {
        /// <summary>這一群叫什麼</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>這一群底下的每一列</summary>
        public List<RowItem> Rows { get; set; } = [];
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
