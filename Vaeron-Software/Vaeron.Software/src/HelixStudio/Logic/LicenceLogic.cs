using HelixStudio.Store;

namespace HelixStudio.Logic;

/// <summary>授權這一頁的純運算</summary>
/// <remarks>不碰資料庫也不碰畫面，給什麼算什麼；沒有狀態，所以是靜態的。畫面照設計檔 DesktopShell.dc.html 的「啟用這台電腦」。</remarks>
public static class LicenceLogic
{
    #region 這把授權算不算數

    /// <summary>這把授權這一刻算不算數：簽章要過，兩邊都有的機器識別要全部對得上，還沒過到期日（當天仍可用）；算數就是它准裝的每一支工具（清單空的就是一支都不准），不算數是 null</summary>
    /// <param name="paper">讀出來的授權與量到的機器識別</param>
    /// <param name="today">今天</param>
    public static List<string>? Allowed(LicenceStore.LicenceView paper, DateOnly today) =>
        paper.Sealed > 0 && paper.Both > 0 && paper.Same == paper.Both
            && paper.Papers.All(one => one.Expires.Length == 0 || (DateOnly.TryParse(one.Expires, out var until) && until >= today))
            ? [.. paper.Papers.Take(paper.Sealed).SelectMany(one => one.Tools)]
            : null;

    #endregion

    #region 啟用這台電腦

    /// <summary>把這台電腦的申請碼、露出來的機器識別與這一刻的操作攤成畫面上的每一格</summary>
    /// <param name="view">讀出來的授權與量到的全部機器識別</param>
    /// <param name="shown">畫面上露出來的那幾項機器識別</param>
    /// <param name="moment">這一刻畫面上發生到哪裡</param>
    /// <param name="icons">圖示名稱對到的每一段路徑</param>
    public static ActivateView Activate(LicenceStore.LicenceView view, IReadOnlyList<DeviceStore.MarkItem> shown,
        MomentItem moment, Dictionary<string, List<string>> icons)
    {
        // 三步：複製過申請碼、貼了東西，各自算完成；第一個還沒完成的是正在做的那一步
        var done = new[] { moment.AskCopied, moment.AskCopied && moment.Pasted, false };
        var active = Enumerable.Range(0, done.Length).First(one => !done[one]);

        return new ActivateView
        {
            // 左欄：三步
            Steps = Step.Select((one, i) => new StepView
            {
                Title = one.Key,
                Text = one.Value,
                Dot = new[] { $"{i + 1}" }.Where(_ => !done[i]).DefaultIfEmpty(string.Empty).First(),
                Paths = new[] { icons["check"] }.Where(_ => done[i]).DefaultIfEmpty([]).First(),
                Back = new[] { "#9ccbf5" }.Where(_ => done[i]).DefaultIfEmpty("Transparent").First(),
                Ink = new[] { "#1f1f1f" }.Where(_ => done[i])
                    .Concat(new[] { "#ffffff" }.Where(_ => i == active))
                    .DefaultIfEmpty("#a0a0a0").First(),
                Line = new[] { "Transparent" }.Where(_ => done[i])
                    .Concat(new[] { "#ffffff" }.Where(_ => i == active))
                    .DefaultIfEmpty("#6b6b6b").First(),
                Thick = new[] { 2.0 }.Where(_ => !done[i] && i == active).DefaultIfEmpty(1.0).First(),
                TitleInk = new[] { "#ffffff" }.Where(_ => done[i] || i == active).DefaultIfEmpty("#b8b8b8").First(),
            }).ToList(),

            // 左欄底部：三條規則
            Rules =
            [
                new RuleView { Paths = icons["wifi-off"], Text = "離線運作，不連任何伺服器" },
                new RuleView { Paths = icons["monitor"], Text = "只綁這台電腦，換機要重新申請" },
                new RuleView { Paths = icons["key"], Text = "沒有帳號密碼，只認賣方給的 KEY" },
            ],

            // 申請碼：原樣，發證工具吃的就是這一段
            Ask = view.Ask,
            CopyLabel = new[] { "已複製" }.Where(_ => moment.Copied).DefaultIfEmpty("複製申請碼").First(),
            CopyPaths = icons[new[] { "check" }.Where(_ => moment.Copied).DefaultIfEmpty("copy").First()],
            CopyInk = new[] { "#107c10" }.Where(_ => moment.Copied).DefaultIfEmpty("#1b1b1b").First(),
            CopyLine = new[] { "#66107c10" }.Where(_ => moment.Copied).DefaultIfEmpty("#24000000").First(),

            // 機器識別：寫出這次量到幾項，只露出那幾項
            MarksNote = $"這次量到 {view.Marks.Count} 項",
            Marks = shown.Select((one, i) => new MarkView
            {
                Label = one.Kind,
                Value = one.Value,
                Line = new[] { "#0f000000" }.Where(_ => i > 0).DefaultIfEmpty("Transparent").First(),
            }).ToList(),

            // 貼上授權驗不過時的框線與原因
            PasteLine = new[] { "#a4262c" }.Where(_ => moment.PasteError.Length > 0).DefaultIfEmpty("#2e000000").First(),
            PasteErrors = new[] { moment.PasteError }.Where(one => one.Length > 0).ToList(),
            ErrorPaths = icons["circle-alert"],
        };
    }

    #endregion

    #region 對照

    /// <summary>左欄三步的標題與說明：設計檔 stepDefs</summary>
    private static IReadOnlyList<KeyValuePair<string, string>> Step { get; } =
    [
        new("複製申請碼", "由這台電腦的機器識別算出"),
        new("交給賣方", "賣方收到後，會回給你一段 KEY"),
        new("貼上授權並套用", "驗過就完成啟用，之後開機直接進入"),
    ];

    #endregion

    #region 模型

    /// <summary>這一刻畫面上發生到哪裡</summary>
    public class MomentItem
    {
        /// <summary>申請碼剛複製，按鈕正顯示已複製</summary>
        public bool Copied { get; set; }

        /// <summary>這一次開著時複製過申請碼</summary>
        public bool AskCopied { get; set; }

        /// <summary>貼上授權那一格有東西</summary>
        public bool Pasted { get; set; }

        /// <summary>套用授權失敗的原因；沒有就是空字串</summary>
        public string PasteError { get; set; } = string.Empty;
    }

    /// <summary>啟用這台電腦這一頁</summary>
    public class ActivateView
    {
        /// <summary>左欄的三步</summary>
        public List<StepView> Steps { get; set; } = [];

        /// <summary>左欄底部的規則</summary>
        public List<RuleView> Rules { get; set; } = [];

        /// <summary>申請碼，每 4 個字一段</summary>
        public string Ask { get; set; } = string.Empty;

        /// <summary>複製按鈕上的字</summary>
        public string CopyLabel { get; set; } = string.Empty;

        /// <summary>複製按鈕的圖示</summary>
        public List<string> CopyPaths { get; set; } = [];

        /// <summary>複製按鈕的字色</summary>
        public string CopyInk { get; set; } = string.Empty;

        /// <summary>複製按鈕的框線色</summary>
        public string CopyLine { get; set; } = string.Empty;

        /// <summary>這次量到幾項</summary>
        public string MarksNote { get; set; } = string.Empty;

        /// <summary>露出來的每一項機器識別</summary>
        public List<MarkView> Marks { get; set; } = [];

        /// <summary>貼上授權那一格的框線色</summary>
        public string PasteLine { get; set; } = string.Empty;

        /// <summary>套用授權失敗的原因；沒有就是零筆</summary>
        public List<string> PasteErrors { get; set; } = [];

        /// <summary>失敗原因的圖示</summary>
        public List<string> ErrorPaths { get; set; } = [];
    }

    /// <summary>左欄的一步</summary>
    public class StepView
    {
        /// <summary>這一步叫什麼</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>這一步的說明</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>圓點裡的號碼；做完了是空字串</summary>
        public string Dot { get; set; } = string.Empty;

        /// <summary>做完了的勾；還沒做完就是零筆</summary>
        public List<string> Paths { get; set; } = [];

        /// <summary>圓點底色</summary>
        public string Back { get; set; } = string.Empty;

        /// <summary>圓點字色</summary>
        public string Ink { get; set; } = string.Empty;

        /// <summary>圓點框線色</summary>
        public string Line { get; set; } = string.Empty;

        /// <summary>圓點框線粗細</summary>
        public double Thick { get; set; }

        /// <summary>標題字色</summary>
        public string TitleInk { get; set; } = string.Empty;
    }

    /// <summary>左欄底部的一條規則</summary>
    public class RuleView
    {
        /// <summary>圖示</summary>
        public List<string> Paths { get; set; } = [];

        /// <summary>規則</summary>
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>機器識別的一列</summary>
    public class MarkView
    {
        /// <summary>哪一種</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>量到的值</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>上方細線的顏色；第一列沒有線</summary>
        public string Line { get; set; } = string.Empty;
    }

    #endregion
}
