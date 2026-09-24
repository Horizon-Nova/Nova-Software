using System.ComponentModel;
using System.Text.Json;
using Models.HelixStudio;

namespace HelixStudio.Logic;

/// <summary>工具目錄這一頁的純運算</summary>
/// <remarks>不碰資料庫也不碰畫面，給什麼算什麼；沒有狀態，所以是靜態的。</remarks>
public static class CatalogLogic
{
    #region 打開工具目錄

    /// <summary>打開工具目錄：統計字、分類、卡片、找不到時那一段</summary>
    /// <param name="rows">vw_shell 查到的每一列</param>
    /// <param name="icons">圖示名稱對到的每一段路徑</param>
    /// <param name="cat">目前選的分類；看全部就是「全部」</param>
    /// <param name="query">搜尋框打的字</param>
    /// <param name="busy">正在跑的工具：代碼對到跑到哪裡</param>
    /// <param name="loaded">資料讀完了沒有</param>
    /// <param name="slow">讀超過 300 毫秒還沒回來</param>
    public static CatalogView Catalog(IReadOnlyList<vw_shell> rows, Dictionary<string, List<string>> icons,
        string cat, string query, IReadOnlyDictionary<string, RunItem> busy, bool loaded, bool slow)
    {
        // 工具是哪幾列；用這台裝置的人有哪幾個身分，決定他能不能用某一支工具
        var tools = rows.Where(row => row.type == "tool").ToList();
        var roles = rows
            .Where(row => row.type == "person")
            .SelectMany(row => JsonSerializer.Deserialize<List<string>>($"{row.roles}") ?? [])
            .Select(one => one.Replace("一般員工", string.Empty))
            .ToList();

        // 裝好的與可更新的，標頭那一行的統計字用得到
        var installed = tools.Where(row => ShellLogic.Installed.Contains($"{row.state}")).ToList();
        var updatable = tools.Where(row => row.state == "update").ToList();

        // 關鍵字比名稱、維護團隊、說明；分類與關鍵字一起套用
        var matched = tools
            .Where(row => new[] { row.name, row.vendor, row.description }.Any(one => $"{one}".Contains(query)))
            .ToList();
        var shown = matched.Where(row => new[] { All, $"{row.cat}" }.Contains(cat)).ToList();

        // 分類：全部，加上資料裡的每一個分類；計數是套用關鍵字之後該分類的工具數
        var chips = Enumerable.Repeat(new ChipItem { Text = All, Count = matched.Count }, 1)
            .Concat(tools
                .Select(row => $"{row.cat}")
                .Distinct()
                .Select(one => new ChipItem { Text = one, Count = matched.Count(row => $"{row.cat}" == one) }))
            .ToList();

        // 現在套著幾個篩選：分類不是全部算一個，關鍵字有字算一個
        var filters = new[] { cat }
            .Where(one => one != All)
            .Concat(new[] { query }.Where(one => one.Trim().Length > 0))
            .Count();

        return new CatalogView
        {
            Note = $"{installed.Count} 支已安裝 · 共 {tools.Count} 支可用"
                 + string.Concat(updatable.Select(_ => $" · {updatable.Count} 支可更新").Take(1)),
            UpdateAllLabel = updatable
                .Select(_ => $"更新這 {updatable.Count} 支")
                .DefaultIfEmpty("全部都是最新版")
                .First(),
            UpdateAllBack = updatable.Select(_ => Accent).DefaultIfEmpty(Dim).First(),
            UpdateAllInk = updatable.Select(_ => OnAccent).DefaultIfEmpty(DimInk).First(),
            Updatables = updatable
                .Select(row => new RunItem { Key = $"{row.tool_no}", Name = $"{row.name}", Action = "update", State = "installed", Version = $"{row.version}", FromState = $"{row.state}", FromVersion = $"{row.installed_version}" })
                .ToList(),
            Query = query,
            Recents = rows.Where(row => row.type == "search").Select(row => $"{row.name}").Take(Remembered).ToList(),
            Chips = chips,
            Selected = chips.FindIndex(one => one.Text == cat),
            Clears = Enumerable.Repeat($"清除篩選（{filters}）", 1).Where(_ => filters > 0).ToList(),
            Cards = shown.Select(row => Card(row, icons, roles, busy)).ToList(),
            Empties = Enumerable.Repeat(new EmptyView
            {
                Text = new[] { query }
                    .Where(one => one.Length > 0)
                    .Select(one => $"「{one}」在{Scope(cat)}裡沒有相符的工具。可能是名稱不同，或這支工具由別的團隊維護。")
                    .DefaultIfEmpty($"{cat}分類目前沒有工具。")
                    .First(),
                AllLabel = $"看全部 {tools.Count} 支工具",
                Paths = icons["search"],
                Suggests = Suggested
                    .Where(one => chips.Any(chip => chip.Text == one))
                    .Select(one => new SuggestItem { Text = $"看「{one}」", Cat = one })
                    .ToList(),
            }, 1).Where(_ => loaded).Where(_ => tools.Count > 0).Where(_ => shown.Count == 0).ToList(),

            // 一支工具都還沒有：第一次用這台裝置
            Firsts = Enumerable.Repeat(new FirstView
            {
                Text = "這台裝置還沒有任何工具。工具由資訊部推到套件庫，也可以自己匯入套件檔。",
                ImportLabel = "匯入工具套件…",
                Paths = icons["package"],
            }, 1).Where(_ => loaded).Where(_ => tools.Count == 0).ToList(),

            // 讀太久才顯示骨架，300 毫秒以內讀完就什麼都不顯示
            Skeletons = Enumerable.Range(0, Skeleton).Where(_ => slow).Where(_ => !loaded).ToList(),
        };
    }

    /// <summary>搜尋範圍寫在說明裡的那幾個字</summary>
    /// <param name="cat">目前選的分類</param>
    private static string Scope(string cat) =>
        new[] { cat }.Where(one => one == All).Select(_ => "全部分類").DefaultIfEmpty(cat).First();

    /// <summary>一支工具在目錄上的那一張卡片</summary>
    /// <param name="row">這一支工具那一列</param>
    /// <param name="icons">圖示名稱對到的每一段路徑</param>
    /// <param name="roles">用這台裝置的人有哪幾個身分</param>
    /// <param name="busy">正在跑的工具：代碼對到跑到哪裡</param>
    private static CardItem Card(vw_shell row, Dictionary<string, List<string>> icons, IReadOnlyList<string> roles,
        IReadOnlyDictionary<string, RunItem> busy)
    {
        // 授權範圍寫著全體員工、寫著他的身分、或寫著已核准，他就用得了這一支
        var allowed = new[] { $"{row.audience}" }
            .SelectMany(one => roles
                .Concat(["全體員工", "已核准"])
                .Select(word => one.Contains(word)))
            .Where(one => one)
            .ToList();

        // 這一支正在跑的話，按鈕改成正在做什麼而且按不下去
        var running = busy.Where(one => one.Key == $"{row.tool_no}").Select(one => one.Value).ToList();

        // 沒在跑的時候，按下去會發生什麼：裝好的開起來、可更新的更新、沒資格的送申請、其餘安裝
        var idle = running.Count == 0;
        var launches = new[] { $"{row.state}" }.Where(_ => idle).Where(one => one == "installed").ToList();
        var updates = new[] { $"{row.state}" }.Where(_ => idle).Where(one => one == "update").ToList();
        var others = new[] { $"{row.state}" }.Where(_ => idle).Where(one => !ShellLogic.Installed.Contains(one)).ToList();

        return new CardItem
        {
            Key = $"{row.tool_no}",
            Name = $"{row.name}",
            Paths = icons[$"{row.icon}"],
            Vendor = $"{row.vendor}",
            Audience = $"{row.audience}",
            Description = $"{row.description}",
            Requireds = new[] { row.required == true }.Where(one => one).ToList(),
            Alloweds = allowed,
            StateText = ShellLogic.StateText[$"{row.state}"],
            StateInk = StateInk[$"{row.state}"],
            StatePaths = icons[StateIcon[$"{row.state}"]],
            Version = new[] { $"{row.state}" }
                .Where(one => one == "update")
                .Select(_ => $"{row.installed_version} → {row.version}")
                .DefaultIfEmpty($"v{row.version}")
                .First(),
            ActionLabel = running
                .Select(one => BusyText[one.Action])
                .DefaultIfEmpty(ActionText.GetValueOrDefault($"{row.state}",
                    allowed.Select(_ => "安裝").DefaultIfEmpty("申請授權").First()))
                .First(),
            ActionBack = running.Select(_ => Dim).DefaultIfEmpty(ActionBack.GetValueOrDefault($"{row.state}", Accent)).First(),
            ActionInk = running.Select(_ => DimInk).DefaultIfEmpty(ActionInk.GetValueOrDefault($"{row.state}", OnAccent)).First(),
            ActionEnabled = idle,
            Runnings = running.ToList(),
            Runs = updates
                .Select(_ => new RunItem { Key = $"{row.tool_no}", Name = $"{row.name}", Action = "update", State = "installed", Version = $"{row.version}", FromState = $"{row.state}", FromVersion = $"{row.installed_version}" })
                .Concat(others
                    .Where(_ => allowed.Count > 0)
                    .Select(_ => new RunItem { Key = $"{row.tool_no}", Name = $"{row.name}", Action = "install", State = "installed", Version = $"{row.version}", FromState = $"{row.state}", FromVersion = $"{row.installed_version}", Needs = ShellLogic.Setup.Approval && row.required != true }))
                .ToList(),
            Opens = launches.Select(_ => $"{row.tool_no}").ToList(),
            Says = others
                .Where(_ => allowed.Count == 0)
                .Select(_ => $"「{row.name}」限「{row.audience}」使用，已把授權申請送給 {row.vendor}")
                .ToList(),
        };
    }

    #endregion

    #region 對照

    /// <summary>看全部分類時那一個分類名</summary>
    public const string All = "全部";

    /// <summary>最近搜尋顯示幾筆</summary>
    public const int Remembered = 4;

    /// <summary>骨架擺幾張卡片</summary>
    private const int Skeleton = 6;

    /// <summary>主色</summary>
    private static string Accent => ShellLogic.Ink["Accent"];

    /// <summary>主色上的字色</summary>
    private static string OnAccent => ShellLogic.Ink["OnAccent"];

    /// <summary>按不下去時的底色</summary>
    private static string Dim => ShellLogic.Ink["WindowBack"];

    /// <summary>按不下去時的字色</summary>
    private static string DimInk => ShellLogic.Ink["TextFaint"];

    /// <summary>找不到工具時建議的分類：設計檔寫死的三個，只留資料裡有的</summary>
    private static readonly string[] Suggested = ["IT 必裝", "生產力", "溝通"];

    /// <summary>每一種狀態的圖示：設計檔 stateMeta</summary>
    public static IReadOnlyDictionary<string, string> StateIcon { get; } = new Dictionary<string, string>
    {
        ["installed"] = "check",
        ["update"] = "arrow-up",
        ["not_installed"] = "circle",
        ["required_missing"] = "triangle-alert",
    };

    /// <summary>每一種狀態的文字色：設計檔 stateMeta</summary>
    public static IReadOnlyDictionary<string, string> StateInk => new Dictionary<string, string>
    {
        ["installed"] = ShellLogic.Ink["TextSecondary"],
        ["update"] = Accent,
        ["not_installed"] = ShellLogic.Ink["TextMuted"],
        ["required_missing"] = ShellLogic.Ink["Danger"],
    };

    /// <summary>做完之後可以撤回的那幾種動作</summary>
    public static IReadOnlyCollection<string> Undoable { get; } = ["remove"];

    /// <summary>做完之後跳出來的那一句話</summary>
    public static IReadOnlyDictionary<string, string> DoneText { get; } = new Dictionary<string, string>
    {
        ["install"] = "已安裝完成",
        ["update"] = "已更新完成",
        ["remove"] = "已移除",
    };

    /// <summary>正在跑的時候那顆動作按鈕的字</summary>
    public static IReadOnlyDictionary<string, string> BusyText { get; } = new Dictionary<string, string>
    {
        ["install"] = "安裝中",
        ["update"] = "更新中",
        ["remove"] = "移除中",
    };

    /// <summary>裝好的工具那顆動作按鈕的字；沒裝的照有沒有資格決定</summary>
    public static IReadOnlyDictionary<string, string> ActionText { get; } = new Dictionary<string, string>
    {
        ["installed"] = "開啟",
        ["update"] = "更新",
    };

    /// <summary>那顆動作按鈕的底色</summary>
    public static IReadOnlyDictionary<string, string> ActionBack => new Dictionary<string, string>
    {
        ["installed"] = ShellLogic.Ink["Surface"],
        ["update"] = Accent,
    };

    /// <summary>那顆動作按鈕的文字色</summary>
    public static IReadOnlyDictionary<string, string> ActionInk => new Dictionary<string, string>
    {
        ["installed"] = ShellLogic.Ink["TextPrimary"],
        ["update"] = OnAccent,
    };

    #endregion

    #region 模型

    /// <summary>工具目錄這一頁</summary>
    public class CatalogView
    {
        /// <summary>標題旁的統計字</summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>更新全部那顆按鈕上的字</summary>
        public string UpdateAllLabel { get; set; } = string.Empty;

        /// <summary>更新全部那顆按鈕的底色</summary>
        public string UpdateAllBack { get; set; } = string.Empty;

        /// <summary>更新全部那顆按鈕的文字色</summary>
        public string UpdateAllInk { get; set; } = string.Empty;

        /// <summary>可以更新的每一支工具，不管現在篩到哪裡</summary>
        public List<RunItem> Updatables { get; set; } = [];

        /// <summary>搜尋框現在的字</summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>最近搜尋過的字，最新的在前</summary>
        public List<string> Recents { get; set; } = [];

        /// <summary>分類</summary>
        public List<ChipItem> Chips { get; set; } = [];

        /// <summary>選中的是第幾個分類</summary>
        public int Selected { get; set; }

        /// <summary>清除篩選那顆按鈕上的字；沒有套篩選就是零筆</summary>
        public List<string> Clears { get; set; } = [];

        /// <summary>這一次看得到的每一張卡片</summary>
        public List<CardItem> Cards { get; set; } = [];

        /// <summary>一張卡片都沒有時的那一段；有卡片就是零筆</summary>
        public List<EmptyView> Empties { get; set; } = [];

        /// <summary>一支工具都還沒有時的那一段；有工具就是零筆</summary>
        public List<FirstView> Firsts { get; set; } = [];

        /// <summary>還在讀而且讀很久時要擺的骨架卡片；其餘是零筆</summary>
        public List<int> Skeletons { get; set; } = [];
    }

    /// <summary>一支工具都還沒有時顯示的那一段</summary>
    public class FirstView
    {
        /// <summary>說明</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>匯入按鈕上的字</summary>
        public string ImportLabel { get; set; } = string.Empty;

        /// <summary>箱子圖示的每一段路徑</summary>
        public List<string> Paths { get; set; } = [];
    }

    /// <summary>一個分類</summary>
    public class ChipItem
    {
        /// <summary>分類名稱</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>這個分類有幾支工具</summary>
        public int Count { get; set; }

        /// <summary>這個分類有工具才點得下去</summary>
        public bool Enabled => Count > 0;
    }

    /// <summary>目錄上的一張卡片</summary>
    public class CardItem
    {
        /// <summary>工具代碼</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>工具名稱</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>圖示的每一段路徑</summary>
        public List<string> Paths { get; set; } = [];

        /// <summary>維護團隊</summary>
        public string Vendor { get; set; } = string.Empty;

        /// <summary>授權範圍</summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>說明</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>IT 必裝的徽章；不是必裝就是零筆</summary>
        public List<bool> Requireds { get; set; } = [];

        /// <summary>符合使用資格的每一個理由；不符資格就是零筆</summary>
        public List<bool> Alloweds { get; set; } = [];

        /// <summary>狀態的字</summary>
        public string StateText { get; set; } = string.Empty;

        /// <summary>狀態的文字色</summary>
        public string StateInk { get; set; } = string.Empty;

        /// <summary>狀態的圖示路徑</summary>
        public List<string> StatePaths { get; set; } = [];

        /// <summary>版本的字</summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>動作按鈕上的字</summary>
        public string ActionLabel { get; set; } = string.Empty;

        /// <summary>動作按鈕的底色</summary>
        public string ActionBack { get; set; } = string.Empty;

        /// <summary>動作按鈕的文字色</summary>
        public string ActionInk { get; set; } = string.Empty;

        /// <summary>沒在跑才按得下去</summary>
        public bool ActionEnabled { get; set; }

        /// <summary>正在跑的那一件事；它身上的百分比自己會通知畫面。沒在跑就是零筆</summary>
        public List<CatalogLogic.RunItem> Runnings { get; set; } = [];

        /// <summary>按下去要跑的那一件事；不跑就是零筆</summary>
        public List<RunItem> Runs { get; set; } = [];

        /// <summary>按下去要開起來的工具代碼；不開就是零筆</summary>
        public List<string> Opens { get; set; } = [];

        /// <summary>按下去只是說一句話時那一句；不說就是零筆</summary>
        public List<string> Says { get; set; } = [];

        /// <summary>這張卡片現在長什麼樣；重畫時拿它比，一樣就不動那一張</summary>
        public string Mark =>
            $"{Key}|{StateText}|{Version}|{ActionLabel}|{ActionBack}|{ActionInk}|{ActionEnabled}|{Runnings.Count}";
    }

    /// <summary>正在跑的一件事</summary>
    public class RunItem : INotifyPropertyChanged
    {
        /// <summary>哪一支工具</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>那一支工具叫什麼</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>在做什麼：install、update、remove</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>做完之後是什麼狀態</summary>
        public string State { get; set; } = string.Empty;

        /// <summary>做完之後裝著的是哪一版</summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>做這件事之前是什麼狀態</summary>
        public string FromState { get; set; } = string.Empty;

        /// <summary>做這件事之前裝著的是哪一版</summary>
        public string FromVersion { get; set; } = string.Empty;

        /// <summary>這一件事要先送主管核可</summary>
        public bool Needs { get; set; }

        /// <summary>跑到百分之幾</summary>
        /// <remarks>這個數字自己會通知畫面；推進度不必把整頁重算重畫一次。</remarks>
        public int Pct
        {
            get => pct;
            set
            {
                pct = value;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Pct)));
            }
        }

        private int pct;

        /// <summary>這一件事身上有值變了</summary>
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>一張卡片都沒有時顯示的那一段</summary>
    public class EmptyView
    {
        /// <summary>說明</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>看全部那顆按鈕上的字</summary>
        public string AllLabel { get; set; } = string.Empty;

        /// <summary>放大鏡的每一段路徑</summary>
        public List<string> Paths { get; set; } = [];

        /// <summary>建議改看哪幾個分類</summary>
        public List<SuggestItem> Suggests { get; set; } = [];
    }

    /// <summary>建議改看的一個分類</summary>
    public class SuggestItem
    {
        /// <summary>按鈕上的字</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>按下去要切到哪一個分類</summary>
        public string Cat { get; set; } = string.Empty;
    }

    #endregion
}
