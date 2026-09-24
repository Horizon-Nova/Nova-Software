using System.Text.Json;
using Models.HelixStudio;

namespace HelixStudio.Logic;

/// <summary>工具資訊這一頁的純運算</summary>
/// <remarks>不碰資料庫也不碰畫面，給什麼算什麼；沒有狀態，所以是靜態的。</remarks>
public static class DetailLogic
{
    #region 打開一支工具的資訊

    /// <summary>打開一支工具的資訊：標頭、按鈕、五個分頁</summary>
    /// <param name="row">這一支工具那一列</param>
    /// <param name="icons">圖示名稱對到的每一段路徑</param>
    /// <param name="roles">用這台裝置的人有哪幾個身分</param>
    /// <param name="dept">用這台裝置的人在哪個部門</param>
    /// <param name="tab">現在看的是哪一個分頁</param>
    /// <param name="busy">正在跑的工具：代碼對到跑到哪裡</param>
    /// <param name="asking">現在有沒有在問「真的要移除嗎」</param>
    public static DetailView Detail(vw_shell row, Dictionary<string, List<string>> icons, IReadOnlyList<string> roles,
        string dept, string tab, IReadOnlyDictionary<string, CatalogLogic.RunItem> busy, bool asking)
    {
        // 授權範圍寫著全體員工、寫著他的身分、或寫著已核准，他就用得了這一支
        var allowed = new[] { $"{row.audience}" }
            .SelectMany(one => roles.Concat(["全體員工", "已核准"]).Select(word => one.Contains(word)))
            .Where(one => one)
            .ToList();

        // 這一支正在跑的話，主要按鈕改成正在做什麼而且按不下去
        var running = busy.Where(one => one.Key == $"{row.tool_no}").Select(one => one.Value).ToList();
        var idle = running.Count == 0;

        // 沒在跑的時候，主要按鈕按下去會發生什麼
        var launches = new[] { $"{row.state}" }.Where(_ => idle).Where(one => one == "installed").ToList();
        var updates = new[] { $"{row.state}" }.Where(_ => idle).Where(one => one == "update").ToList();
        var others = new[] { $"{row.state}" }.Where(_ => idle).Where(one => !ShellLogic.Installed.Contains(one)).ToList();

        // 已經裝好的那一支，次要按鈕是移除；沒裝的是回工具目錄
        var installed = new[] { $"{row.state}" }.Where(one => ShellLogic.Installed.Contains(one)).ToList();

        // 五個分頁，現在看的是哪一個
        var tabs = Tabs.Select(one => new TabItem { Key = one.Key, Text = one.Value }).ToList();
        var shown = tabs.Where(one => one.Key == tab).Select(one => one.Key).ToList();

        // 必裝的工具要移除之前得先問一次
        var confirms = installed
            .Where(_ => row.required == true)
            .Select(_ => new ConfirmView
            {
                Title = $"移除「{row.name}」會讓這台裝置不合規",
                Text = "這是公司政策要求安裝的工具。移除後資訊部會收到通知，7 日內未重新安裝將限制內網存取。",
                Hint = $"請輸入工具名稱「{row.name}」以確認",
                Name = $"{row.name}",
                OkLabel = $"移除 {row.name}",
                Run = Run(row, "remove"),
            })
            .ToList();

        // 這一支的操作說明；沒有說明的工具是零筆
        var guides = new[] { $"{row.guide}" }
            .Where(one => one.Length > 0 && one != "null")
            .Select(one => JsonSerializer.Deserialize<GuideRow>(one)!)
            .ToList();

        return new DetailView
        {
            Key = $"{row.tool_no}",
            Name = $"{row.name}",
            Crumb = $"— {row.name} · 工具資訊",
            Paths = icons[$"{row.icon}"],
            BackPaths = icons["chevron-left"],
            Requireds = new[] { row.required == true }.Where(one => one).ToList(),
            StateText = ShellLogic.StateText[$"{row.state}"],
            StateInk = CatalogLogic.StateInk[$"{row.state}"],
            StatePaths = icons[CatalogLogic.StateIcon[$"{row.state}"]],
            Meta = $"{row.vendor} · {row.cat}",
            Description = $"{row.description}",

            PrimaryLabel = running
                .Select(one => CatalogLogic.BusyText[one.Action])
                .DefaultIfEmpty(updates.Select(_ => $"更新到 {row.version}")
                    .Concat(launches.Select(_ => "開啟工具"))
                    .Concat(others.Select(_ => allowed.Select(one => "安裝").DefaultIfEmpty("申請授權").First()))
                    .DefaultIfEmpty(string.Empty)
                    .First())
                .First(),
            PrimaryBack = running.Select(_ => Dim).DefaultIfEmpty(Accent).First(),
            PrimaryInk = running.Select(_ => DimInk).DefaultIfEmpty(OnAccent).First(),
            PrimaryEnabled = idle,
            Runs = updates
                .Select(_ => Run(row, "update"))
                .Concat(others.Where(_ => allowed.Count > 0).Select(_ => Run(row, "install")))
                .ToList(),
            Opens = launches.Select(_ => $"{row.tool_no}").ToList(),
            Says = others
                .Where(_ => allowed.Count == 0)
                .Select(_ => $"已把「{row.name}」的授權申請送給 {row.vendor}")
                .ToList(),

            SecondaryLabel = installed.Select(_ => "移除工具").DefaultIfEmpty("回工具目錄").First(),
            SecondaryInk = installed.Select(_ => Danger).DefaultIfEmpty(Ink).First(),
            SecondaryLine = installed.Select(_ => DangerLine).DefaultIfEmpty(HairLine).First(),
            Removes = installed.Where(_ => row.required != true).Select(_ => Run(row, "remove")).ToList(),
            Confirms = confirms,
            Backs = Enumerable.Repeat(true, 1).Where(_ => installed.Count == 0).ToList(),
            Asking = confirms.Where(_ => asking).ToList(),
            Runnings = running.ToList(),

            Tabs = tabs,
            Selected = tabs.FindIndex(one => one.Key == tab),

            // 總覽：這支工具做得到什麼，還有它的基本資料
            Overviews = shown.Where(one => one == "overview").Select(_ => new OverviewView
            {
                CheckPaths = icons["check"],
                Capabilities = Listed($"{row.capabilities}").Select((one, at) => new TextRow { Text = one, Line = Line(at) }).ToList(),
                Facts = new List<FactRow>
                {
                    new()
                    {
                        Label = "版本",
                        Value = $"v{row.version}"
                              + string.Concat(updates.Select(_ => $"（已安裝 {row.installed_version}）").Take(1)),
                        Mono = MonoFont,
                    },
                    new() { Label = "更新日期", Value = $"{row.updated}", Mono = MonoFont },
                    new() { Label = "安裝大小", Value = $"{row.size}", Mono = MonoFont },
                    new() { Label = "自動更新", Value = AutoText[row.auto_update == true], Mono = UiFont },
                    new() { Label = "維護團隊", Value = $"{row.vendor}", Mono = UiFont },
                }.Select((one, at) => new FactRow { Label = one.Label, Value = one.Value, Mono = one.Mono, Line = Line(at) }).ToList(),
            }).ToList(),

            // 怎麼用：維護團隊寫的操作說明
            Guides = shown.Where(one => one == "guide").SelectMany(_ => guides).Select(one => new GuideView
            {
                Checklist = one.checklist.Select((text, at) => new TextRow { Text = text, Line = Line(at) }).ToList(),
                Steps = one.steps.Select((step, at) => new StepRow
                {
                    No = $"{at + 1}",
                    Title = step.t,
                    Text = step.d,
                    Shot = step.shot,
                    Line = Line(at),
                }).ToList(),
                Video = one.video,
                Can = one.can.Select((text, at) => new TextRow { Text = text, Line = Line(at) }).ToList(),
                Cannot = one.cannot.Select((text, at) => new TextRow { Text = text, Line = Line(at) }).ToList(),
                Faq = one.faq.Select((ask, at) => new AskRow { Ask = ask.q, Answer = ask.a, Line = Line(at) }).ToList(),
                Sop = one.sop,
                Owner = $"本說明由 {one.owner} 維護 · 內線 {one.ext} · 最後更新 {one.updated}",
                ReportText = $"已把「{row.name}」的問題回報給 {one.owner}，內線 {one.ext}",
                SopText = $"桌面版會開啟知識庫的「{one.sop}」",
                CheckPaths = icons["check"],
                CrossPaths = icons["x"],
            }).ToList(),

            // 沒有操作說明的工具，怎麼用那一頁只說一句話
            Blanks = shown.Where(one => one == "guide").Where(_ => guides.Count == 0).Select(_ => new BlankView
            {
                Text = $"這支工具的操作說明還沒填寫。說明由各工具的維護團隊撰寫，可以先問 AI 助理，或直接聯絡 {row.vendor}。",
                ReportText = $"已把「{row.name}」的問題回報給 {row.vendor}",
            }).ToList(),

            // 權限與授權：這支工具會用到什麼，還有誰可以用
            Perms = shown.Where(one => one == "perm").Select(_ => new PermView
            {
                Permissions = Listed($"{row.permissions}").Select((one, at) => new TextRow { Text = one, Line = Line(at) }).ToList(),
                LockPaths = icons["lock"],
                Audience = $"{row.audience}",
                EligibleLabel = allowed.Select(_ => "你符合使用資格").DefaultIfEmpty("你目前不在授權範圍").First(),
                EligibleInk = allowed.Select(_ => Ink2).DefaultIfEmpty(Danger).First(),
                EligibleBack = allowed.Select(_ => Dim).DefaultIfEmpty(DangerBack).First(),
                EligibleLine = allowed.Select(_ => HairLine).DefaultIfEmpty(DangerLine).First(),
                EligiblePaths = allowed.Select(_ => icons["check"]).DefaultIfEmpty(icons["triangle-alert"]).First(),
                EligibleNote = allowed
                    .Select(_ => $"你的身分：{dept}。授權由 {row.vendor} 管理，人事異動會自動同步。")
                    .DefaultIfEmpty($"需要使用請按「申請授權」，由 {row.vendor} 核准後自動配發席次。")
                    .First(),
            }).ToList(),

            // 更新日誌：照原本的順序列出來
            Logs = shown.Where(one => one == "log").Select(_ => new LogView
            {
                Rows = JsonSerializer.Deserialize<List<LogRow>>($"{row.changelog}")!
                    .Select((one, at) => new LogItem
                    {
                        Ver = one.ver,
                        Date = one.date,
                        Note = one.note,
                        Latests = Enumerable.Repeat(true, 1).Where(_ => at == 0).ToList(),
                        Line = Line(at),
                    })
                    .ToList(),
            }).ToList(),

            // 席次：配出去幾席、還剩幾席
            Seats = shown.Where(one => one == "seats").Select(_ => JsonSerializer.Deserialize<SeatRow>($"{row.seats}")!).Select(one =>
            {
                var pct = (int)Math.Round(one.used * 100.0 / one.total);
                var tight = Enumerable.Repeat(true, 1).Where(_ => pct > Crowded).ToList();

                return new SeatView
                {
                    Used = $"{one.used}",
                    Total = $"/ {one.total} 席已配發",
                    Pct = pct,
                    PctText = $"{pct}%",
                    PctInk = tight.Select(_ => Danger).DefaultIfEmpty(DimInk).First(),
                    FillInk = tight.Select(_ => Danger).DefaultIfEmpty(Accent).First(),
                    Note = tight
                        .Select(_ => $"席次快用完了，只剩 {one.total - one.used} 席。要擴充請聯絡 {row.vendor}。")
                        .DefaultIfEmpty($"還有 {one.total - one.used} 席可配發。席次由 {row.vendor} 依部門核配。")
                        .First(),
                };
            }).ToList(),
        };
    }

    /// <summary>這一支工具做某一件事之後會變成什麼樣</summary>
    /// <param name="row">這一支工具那一列</param>
    /// <param name="action">要做的事：install、update、remove</param>
    private static CatalogLogic.RunItem Run(vw_shell row, string action) =>
        new()
        {
            Key = $"{row.tool_no}",
            Name = $"{row.name}",
            Action = action,
            State = Result.GetValueOrDefault(action, MissingState[row.required == true]),
            Version = Result.ContainsKey(action) ? $"{row.version}" : string.Empty,
            FromState = $"{row.state}",
            FromVersion = $"{row.installed_version}",
            Needs = ShellLogic.Setup.Approval && row.required != true && action == "install",
        };

    /// <summary>jsonb 存的字串陣列攤成一串</summary>
    /// <param name="json">那一欄的內容</param>
    private static List<string> Listed(string json) =>
        JsonSerializer.Deserialize<List<string>>(json) ?? [];

    /// <summary>第幾列的分隔線：第一列不畫</summary>
    /// <param name="at">第幾列，從 0 起算</param>
    private static string Line(int at) =>
        new[] { at }.Where(one => one == 0).Select(_ => "0").DefaultIfEmpty("0,1,0,0").First();

    #endregion

    #region 對照

    /// <summary>主色</summary>
    private static string Accent => ShellLogic.Ink["Accent"];

    /// <summary>主色上的字色</summary>
    private static string OnAccent => ShellLogic.Ink["OnAccent"];

    /// <summary>按不下去時的底色</summary>
    private static string Dim => ShellLogic.Ink["WindowBack"];

    /// <summary>按不下去時的字色</summary>
    private static string DimInk => ShellLogic.Ink["TextMuted"];

    /// <summary>一般字色</summary>
    private static string Ink => ShellLogic.Ink["TextPrimary"];

    /// <summary>次一級字色</summary>
    private static string Ink2 => ShellLogic.Ink["TextSecondary"];

    /// <summary>危險色</summary>
    private static string Danger => ShellLogic.Ink["Danger"];

    /// <summary>危險色的底</summary>
    private static string DangerBack => ShellLogic.Ink["DangerBack"];

    /// <summary>危險色的框線</summary>
    private static string DangerLine => ShellLogic.Ink["DangerLine"];

    /// <summary>一般框線</summary>
    private static string HairLine => ShellLogic.Ink["HairLine"];

    /// <summary>等寬字</summary>
    private const string MonoFont = "Cascadia Mono, Consolas";

    /// <summary>介面字</summary>
    private const string UiFont = "Segoe UI Variable Text, Segoe UI, Microsoft JhengHei UI, Noto Sans TC";

    /// <summary>席次用到幾成算快用完</summary>
    private const int Crowded = 90;

    /// <summary>五個分頁，順序照設計檔</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> Tabs { get; } =
    [
        new("overview", "總覽"),
        new("guide", "怎麼用"),
        new("perm", "權限與授權"),
        new("log", "更新日誌"),
        new("seats", "席次與使用量"),
    ];

    /// <summary>裝好與更新完之後的狀態</summary>
    private static IReadOnlyDictionary<string, string> Result { get; } = new Dictionary<string, string>
    {
        ["install"] = "installed",
        ["update"] = "installed",
    };

    /// <summary>移除之後的狀態：必裝的移掉就是必裝未安裝</summary>
    private static IReadOnlyDictionary<bool, string> MissingState { get; } = new Dictionary<bool, string>
    {
        [true] = "required_missing",
        [false] = "not_installed",
    };

    /// <summary>自動更新那一列寫什麼</summary>
    private static IReadOnlyDictionary<bool, string> AutoText { get; } = new Dictionary<bool, string>
    {
        [true] = "開啟，由資訊部管理",
        [false] = "關閉，需手動更新",
    };

    #endregion

    #region 模型

    /// <summary>工具資訊這一頁</summary>
    public class DetailView
    {
        /// <summary>工具代碼</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>工具名稱</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>標題列的麵包屑</summary>
        public string Crumb { get; set; } = string.Empty;

        /// <summary>工具圖示的每一段路徑</summary>
        public List<string> Paths { get; set; } = [];

        /// <summary>返回箭頭的每一段路徑</summary>
        public List<string> BackPaths { get; set; } = [];

        /// <summary>IT 必裝的徽章；不是必裝就是零筆</summary>
        public List<bool> Requireds { get; set; } = [];

        /// <summary>狀態的字</summary>
        public string StateText { get; set; } = string.Empty;

        /// <summary>狀態的文字色</summary>
        public string StateInk { get; set; } = string.Empty;

        /// <summary>狀態的圖示路徑</summary>
        public List<string> StatePaths { get; set; } = [];

        /// <summary>維護團隊與分類</summary>
        public string Meta { get; set; } = string.Empty;

        /// <summary>說明</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>主要按鈕上的字</summary>
        public string PrimaryLabel { get; set; } = string.Empty;

        /// <summary>主要按鈕的底色</summary>
        public string PrimaryBack { get; set; } = string.Empty;

        /// <summary>主要按鈕的文字色</summary>
        public string PrimaryInk { get; set; } = string.Empty;

        /// <summary>沒在跑才按得下去</summary>
        public bool PrimaryEnabled { get; set; }

        /// <summary>主要按鈕按下去要跑的那一件事；不跑就是零筆</summary>
        public List<CatalogLogic.RunItem> Runs { get; set; } = [];

        /// <summary>主要按鈕按下去要開起來的工具代碼；不開就是零筆</summary>
        public List<string> Opens { get; set; } = [];

        /// <summary>主要按鈕按下去只是說一句話時那一句；不說就是零筆</summary>
        public List<string> Says { get; set; } = [];

        /// <summary>次要按鈕上的字</summary>
        public string SecondaryLabel { get; set; } = string.Empty;

        /// <summary>次要按鈕的文字色</summary>
        public string SecondaryInk { get; set; } = string.Empty;

        /// <summary>次要按鈕的框線色</summary>
        public string SecondaryLine { get; set; } = string.Empty;

        /// <summary>次要按鈕按下去直接移除的那一件事；不直接移除就是零筆</summary>
        public List<CatalogLogic.RunItem> Removes { get; set; } = [];

        /// <summary>次要按鈕按下去要先確認的那一次；不必確認就是零筆</summary>
        public List<ConfirmView> Confirms { get; set; } = [];

        /// <summary>次要按鈕按下去是回工具目錄；不是就是零筆</summary>
        public List<bool> Backs { get; set; } = [];

        /// <summary>正在問「真的要移除嗎」的那一次；沒在問就是零筆</summary>
        public List<ConfirmView> Asking { get; set; } = [];

        /// <summary>正在跑的那一件事；它身上的百分比自己會通知畫面。沒在跑就是零筆</summary>
        public List<CatalogLogic.RunItem> Runnings { get; set; } = [];

        /// <summary>五個分頁</summary>
        public List<TabItem> Tabs { get; set; } = [];

        /// <summary>現在看的是第幾個分頁</summary>
        public int Selected { get; set; }

        /// <summary>總覽分頁；沒看這一頁就是零筆</summary>
        public List<OverviewView> Overviews { get; set; } = [];

        /// <summary>怎麼用分頁；沒看這一頁或沒有說明就是零筆</summary>
        public List<GuideView> Guides { get; set; } = [];

        /// <summary>沒有說明時怎麼用那一頁的那一段；其餘是零筆</summary>
        public List<BlankView> Blanks { get; set; } = [];

        /// <summary>權限與授權分頁；沒看這一頁就是零筆</summary>
        public List<PermView> Perms { get; set; } = [];

        /// <summary>更新日誌分頁；沒看這一頁就是零筆</summary>
        public List<LogView> Logs { get; set; } = [];

        /// <summary>席次分頁；沒看這一頁就是零筆</summary>
        public List<SeatView> Seats { get; set; } = [];
    }

    /// <summary>一個分頁</summary>
    public class TabItem
    {
        /// <summary>分頁代碼</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>分頁上的字</summary>
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>總覽分頁</summary>
    public class OverviewView
    {
        /// <summary>打勾圖示的每一段路徑</summary>
        public List<string> CheckPaths { get; set; } = [];

        /// <summary>這支工具做得到的每一件事</summary>
        public List<TextRow> Capabilities { get; set; } = [];

        /// <summary>基本資料的每一列</summary>
        public List<FactRow> Facts { get; set; } = [];
    }

    /// <summary>只有一段文字的一列</summary>
    public class TextRow
    {
        /// <summary>這一列寫什麼</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>這一列上面那條分隔線</summary>
        public string Line { get; set; } = string.Empty;
    }

    /// <summary>基本資料的一列</summary>
    public class FactRow
    {
        /// <summary>左邊的標籤</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>右邊的值</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>這個值用哪一種字</summary>
        public string Mono { get; set; } = string.Empty;

        /// <summary>這一列上面那條分隔線</summary>
        public string Line { get; set; } = string.Empty;
    }

    /// <summary>怎麼用分頁</summary>
    public class GuideView
    {
        /// <summary>操作前要先確認的每一項</summary>
        public List<TextRow> Checklist { get; set; } = [];

        /// <summary>操作步驟</summary>
        public List<StepRow> Steps { get; set; } = [];

        /// <summary>示範影片放置框裡的字</summary>
        public string Video { get; set; } = string.Empty;

        /// <summary>這支工具做得到的每一件事</summary>
        public List<TextRow> Can { get; set; } = [];

        /// <summary>做不到、要走別的流程的每一件事</summary>
        public List<TextRow> Cannot { get; set; } = [];

        /// <summary>出問題時怎麼辦</summary>
        public List<AskRow> Faq { get; set; } = [];

        /// <summary>現場 SOP 的名稱</summary>
        public string Sop { get; set; } = string.Empty;

        /// <summary>這份說明由誰維護</summary>
        public string Owner { get; set; } = string.Empty;

        /// <summary>回報給維護團隊之後說的那一句</summary>
        public string ReportText { get; set; } = string.Empty;

        /// <summary>按下 SOP 之後說的那一句</summary>
        public string SopText { get; set; } = string.Empty;

        /// <summary>打勾圖示的每一段路徑</summary>
        public List<string> CheckPaths { get; set; } = [];

        /// <summary>打叉圖示的每一段路徑</summary>
        public List<string> CrossPaths { get; set; } = [];
    }

    /// <summary>操作步驟的一列</summary>
    public class StepRow
    {
        /// <summary>第幾步</summary>
        public string No { get; set; } = string.Empty;

        /// <summary>這一步在做什麼</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>這一步的說明</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>截圖放置框裡的字</summary>
        public string Shot { get; set; } = string.Empty;

        /// <summary>這一列上面那條分隔線</summary>
        public string Line { get; set; } = string.Empty;
    }

    /// <summary>一問一答</summary>
    public class AskRow
    {
        /// <summary>問題</summary>
        public string Ask { get; set; } = string.Empty;

        /// <summary>回答</summary>
        public string Answer { get; set; } = string.Empty;

        /// <summary>這一列上面那條分隔線</summary>
        public string Line { get; set; } = string.Empty;
    }

    /// <summary>沒有操作說明時的那一段</summary>
    public class BlankView
    {
        /// <summary>說明</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>請維護團隊補上說明之後說的那一句</summary>
        public string ReportText { get; set; } = string.Empty;
    }

    /// <summary>權限與授權分頁</summary>
    public class PermView
    {
        /// <summary>這個工具會用到的每一項</summary>
        public List<TextRow> Permissions { get; set; } = [];

        /// <summary>鎖頭圖示的每一段路徑</summary>
        public List<string> LockPaths { get; set; } = [];

        /// <summary>授權範圍原文</summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>符不符合資格那一行</summary>
        public string EligibleLabel { get; set; } = string.Empty;

        /// <summary>那一行的文字色</summary>
        public string EligibleInk { get; set; } = string.Empty;

        /// <summary>那一行圖示圈的底色</summary>
        public string EligibleBack { get; set; } = string.Empty;

        /// <summary>那一行圖示圈的框線色</summary>
        public string EligibleLine { get; set; } = string.Empty;

        /// <summary>那一行的圖示路徑</summary>
        public List<string> EligiblePaths { get; set; } = [];

        /// <summary>那一行下面的說明</summary>
        public string EligibleNote { get; set; } = string.Empty;
    }

    /// <summary>更新日誌分頁</summary>
    public class LogView
    {
        /// <summary>每一版</summary>
        public List<LogItem> Rows { get; set; } = [];
    }

    /// <summary>更新日誌的一列</summary>
    public class LogItem
    {
        /// <summary>版號</summary>
        public string Ver { get; set; } = string.Empty;

        /// <summary>日期</summary>
        public string Date { get; set; } = string.Empty;

        /// <summary>這一版改了什麼</summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>最新那一版的標籤；其餘是零筆</summary>
        public List<bool> Latests { get; set; } = [];

        /// <summary>這一列上面那條分隔線</summary>
        public string Line { get; set; } = string.Empty;
    }

    /// <summary>席次分頁</summary>
    public class SeatView
    {
        /// <summary>配出去幾席</summary>
        public string Used { get; set; } = string.Empty;

        /// <summary>總共幾席</summary>
        public string Total { get; set; } = string.Empty;

        /// <summary>配出去幾成</summary>
        public int Pct { get; set; }

        /// <summary>幾成那幾個字</summary>
        public string PctText { get; set; } = string.Empty;

        /// <summary>幾成那幾個字的顏色</summary>
        public string PctInk { get; set; } = string.Empty;

        /// <summary>長條填色</summary>
        public string FillInk { get; set; } = string.Empty;

        /// <summary>下面那一句</summary>
        public string Note { get; set; } = string.Empty;
    }

    /// <summary>移除必裝工具前要先確認的那一次</summary>
    public class ConfirmView
    {
        /// <summary>標題</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>說明</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>輸入框上面那一行提示</summary>
        public string Hint { get; set; } = string.Empty;

        /// <summary>要打對的工具名稱</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>確認按鈕上的字</summary>
        public string OkLabel { get; set; } = string.Empty;

        /// <summary>確認之後要跑的那一件事</summary>
        public CatalogLogic.RunItem Run { get; set; } = new();
    }

    /// <summary>資料庫 guide 那一欄的內容</summary>
    public class GuideRow
    {
        /// <summary>這份說明由誰維護</summary>
        public string owner { get; set; } = string.Empty;

        /// <summary>內線</summary>
        public string ext { get; set; } = string.Empty;

        /// <summary>最後更新</summary>
        public string updated { get; set; } = string.Empty;

        /// <summary>現場 SOP 的名稱</summary>
        public string sop { get; set; } = string.Empty;

        /// <summary>示範影片放置框裡的字</summary>
        public string video { get; set; } = string.Empty;

        /// <summary>操作前要先確認的每一項</summary>
        public List<string> checklist { get; set; } = [];

        /// <summary>操作步驟</summary>
        public List<StepJson> steps { get; set; } = [];

        /// <summary>做得到的每一件事</summary>
        public List<string> can { get; set; } = [];

        /// <summary>做不到的每一件事</summary>
        public List<string> cannot { get; set; } = [];

        /// <summary>出問題時怎麼辦</summary>
        public List<AskJson> faq { get; set; } = [];
    }

    /// <summary>資料庫 guide.steps 的一項</summary>
    public class StepJson
    {
        /// <summary>這一步在做什麼</summary>
        public string t { get; set; } = string.Empty;

        /// <summary>這一步的說明</summary>
        public string d { get; set; } = string.Empty;

        /// <summary>截圖放置框裡的字</summary>
        public string shot { get; set; } = string.Empty;
    }

    /// <summary>資料庫 guide.faq 的一項</summary>
    public class AskJson
    {
        /// <summary>問題</summary>
        public string q { get; set; } = string.Empty;

        /// <summary>回答</summary>
        public string a { get; set; } = string.Empty;
    }

    /// <summary>資料庫 changelog 的一項</summary>
    public class LogRow
    {
        /// <summary>版號</summary>
        public string ver { get; set; } = string.Empty;

        /// <summary>日期</summary>
        public string date { get; set; } = string.Empty;

        /// <summary>這一版改了什麼</summary>
        public string note { get; set; } = string.Empty;
    }

    /// <summary>資料庫 seats 那一欄的內容</summary>
    public class SeatRow
    {
        /// <summary>配出去幾席</summary>
        public int used { get; set; }

        /// <summary>總共幾席</summary>
        public int total { get; set; }
    }

    #endregion
}
