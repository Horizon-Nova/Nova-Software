using Models.HelixStudio;

namespace HelixStudio.Logic;

/// <summary>AI 助理這一頁的純運算</summary>
/// <remarks>不碰資料庫也不碰畫面，給什麼算什麼；沒有狀態，所以是靜態的。</remarks>
public static class AssistLogic
{
    #region 打開 AI 助理

    /// <summary>打開 AI 助理：標頭、現在在哪、問過的每一則、快速提問</summary>
    /// <param name="rows">vw_assist 查到的每一列</param>
    /// <param name="tool">AI 助理那一支工具的那一列</param>
    /// <param name="icons">圖示名稱對到的每一段路徑</param>
    /// <param name="where">現在停在哪一個畫面</param>
    /// <param name="draft">輸入框打的字</param>
    /// <param name="thinking">正在查資料</param>
    public static AssistView Assist(IReadOnlyList<vw_assist> rows, vw_shell tool,
        Dictionary<string, List<string>> icons, string where, string draft, bool thinking)
    {
        // 問答與快速提問分別是哪幾列
        var said = rows.Where(row => row.type == "message").ToList();
        var prompts = rows.Where(row => row.type == "prompt").ToList();

        // 打了字而且沒有正在查，送出鈕才亮起來
        var ready = draft.Trim().Length > 0 && !thinking;

        return new AssistView
        {
            Name = $"{tool.name}",
            Sub = $"v{tool.version} · 離線模型 · 不外連",
            Paths = icons[$"{tool.icon}"],
            ClosePaths = icons["x"],
            Where = $"目前作業：{where}（問 AI 不會離開這個畫面）",

            // 還沒問過任何事，先說這個助理能做什麼
            Intros = Enumerable.Repeat(new IntroView
            {
                Title = "問我這台機器、料號或系統怎麼操作",
                Text = "回答只取自公司內部手冊、SOP 與歷史工單；問答時右邊的作業畫面不會被關掉。",
            }, 1).Where(_ => said.Count == 0).ToList(),

            Said = said.Select(row => new SaidItem
            {
                Text = $"{row.text}",
                Side = Side[$"{row.who}"],
                Back = Back[$"{row.who}"],
                Ink = Ink[$"{row.who}"],
                Cites = new[] { $"{row.cite}" }.Where(one => one.Length > 0).Select(one => $"來源：{one}").ToList(),
            }).ToList(),

            Thinkings = Enumerable.Repeat("正在查公司手冊與工單…", 1).Where(_ => thinking).ToList(),
            Prompts = prompts.Select(row => $"{row.text}").ToList(),

            Draft = draft,
            Hint = "輸入問題，Enter 送出",
            SendLabel = "送出",
            SendBack = new[] { ready }.Where(one => one).Select(_ => ShellLogic.Ink["Accent"]).DefaultIfEmpty(ShellLogic.Ink["WindowBack"]).First(),
            SendInk = new[] { ready }.Where(one => one).Select(_ => ShellLogic.Ink["OnAccent"]).DefaultIfEmpty(ShellLogic.Ink["TextFaint"]).First(),
            SendEnabled = ready,
            Note = "AI 回答僅供參考，作業前請以現場 SOP 與主管指示為準。",
        };
    }

    /// <summary>問一句：把問的那一句與助理的回答各寫成一列</summary>
    /// <param name="ask">問的那一句</param>
    /// <param name="where">現在停在哪一個畫面</param>
    /// <param name="cite">回答要附的來源</param>
    public static List<SayItem> Say(string ask, string where, string cite) =>
        [
            new() { Who = "me", Text = ask.Trim(), Cite = string.Empty },
            new()
            {
                Who = "ai",
                Text = $"你現在開著「{where}」，這段回答來自公司內部手冊與工單紀錄（離線模型，不外連）。"
                     + "回答時你的作業會留在原畫面，隨時可以收起這個面板繼續做。",
                Cite = cite,
            },
        ];

    #endregion

    #region 對照

    /// <summary>誰說的那一則靠哪一邊</summary>
    private static IReadOnlyDictionary<string, string> Side { get; } = new Dictionary<string, string>
    {
        ["me"] = "Right",
        ["ai"] = "Left",
    };

    /// <summary>誰說的那一則什麼底色</summary>
    private static IReadOnlyDictionary<string, string> Back => new Dictionary<string, string>
    {
        ["me"] = ShellLogic.Ink["Accent"],
        ["ai"] = ShellLogic.Ink["WindowBack"],
    };

    /// <summary>誰說的那一則什麼字色</summary>
    private static IReadOnlyDictionary<string, string> Ink => new Dictionary<string, string>
    {
        ["me"] = ShellLogic.Ink["OnAccent"],
        ["ai"] = ShellLogic.Ink["TextPrimary"],
    };

    #endregion

    #region 模型

    /// <summary>AI 助理這一頁</summary>
    public class AssistView
    {
        /// <summary>助理叫什麼</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>版本與它跑在哪裡</summary>
        public string Sub { get; set; } = string.Empty;

        /// <summary>助理圖示的每一段路徑</summary>
        public List<string> Paths { get; set; } = [];

        /// <summary>收起按鈕的每一段路徑</summary>
        public List<string> ClosePaths { get; set; } = [];

        /// <summary>現在停在哪一個畫面</summary>
        public string Where { get; set; } = string.Empty;

        /// <summary>還沒問過事情時的那一段；問過就是零筆</summary>
        public List<IntroView> Intros { get; set; } = [];

        /// <summary>問過與答過的每一則</summary>
        public List<SaidItem> Said { get; set; } = [];

        /// <summary>正在查資料時的那一句；沒在查就是零筆</summary>
        public List<string> Thinkings { get; set; } = [];

        /// <summary>快速提問</summary>
        public List<string> Prompts { get; set; } = [];

        /// <summary>輸入框現在的字</summary>
        public string Draft { get; set; } = string.Empty;

        /// <summary>輸入框沒有字時顯示的提示</summary>
        public string Hint { get; set; } = string.Empty;

        /// <summary>送出鈕上的字</summary>
        public string SendLabel { get; set; } = string.Empty;

        /// <summary>送出鈕的底色</summary>
        public string SendBack { get; set; } = string.Empty;

        /// <summary>送出鈕的文字色</summary>
        public string SendInk { get; set; } = string.Empty;

        /// <summary>打了字而且沒在查才按得下去</summary>
        public bool SendEnabled { get; set; }

        /// <summary>面板最下面那一句</summary>
        public string Note { get; set; } = string.Empty;
    }

    /// <summary>還沒問過事情時的那一段</summary>
    public class IntroView
    {
        /// <summary>標題</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>說明</summary>
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>問答裡的一則</summary>
    public class SaidItem
    {
        /// <summary>說了什麼</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>靠哪一邊</summary>
        public string Side { get; set; } = string.Empty;

        /// <summary>底色</summary>
        public string Back { get; set; } = string.Empty;

        /// <summary>字色</summary>
        public string Ink { get; set; } = string.Empty;

        /// <summary>附的來源；沒有就是零筆</summary>
        public List<string> Cites { get; set; } = [];
    }

    /// <summary>要寫進資料庫的一則</summary>
    public class SayItem
    {
        /// <summary>誰說的</summary>
        public string Who { get; set; } = string.Empty;

        /// <summary>說了什麼</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>附的來源</summary>
        public string Cite { get; set; } = string.Empty;
    }

    #endregion
}
