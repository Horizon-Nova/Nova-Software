using Models.HelixStudio;

namespace HelixStudio.Logic;

/// <summary>日誌這一頁：把 view 的欄位擺到畫面上</summary>
/// <remarks>過濾、計數、排序、時間的字都在 view 與查詢裡做完；這裡只把欄位搬到畫面要的格子，算「幾分前」。</remarks>
public static class LogLogic
{
    #region 打開日誌

    /// <summary>打開日誌：頁首的筆數、欄名的排序箭頭、擺出來的那幾列、選中那一件的明細、空的時候那一段</summary>
    /// <param name="rows">vw_event_log：每一種事件幾筆，與照搜尋字查出、照欄排好的每一件</param>
    /// <param name="icons">圖示名稱對到的每一段路徑</param>
    /// <param name="query">搜尋框打的字</param>
    /// <param name="sort">照哪一欄排：at、type、tool、who</param>
    /// <param name="descending">由大到小</param>
    /// <param name="picked">選中那一件的流水號；還沒選過是 0</param>
    /// <param name="copied">選中那一件剛被複製，按鈕換成勾號</param>
    /// <param name="now">現在的時刻（UTC），算「幾分前」用</param>
    public static LogView Log(IReadOnlyList<vw_event_log> rows, Dictionary<string, List<string>> icons, string query, string sort, bool descending,
        int picked, bool copied, DateTime now)
    {
        // 每一種事件幾筆，view 已經照筆數排好；全部幾筆是它們加起來
        var counts = rows.Where(row => row.type == "count").ToList();
        var total = counts.Sum(row => row.event_count ?? 0);

        // 照搜尋字查出來的每一件，全部列出
        var events = rows.Where(row => row.type == "event").ToList();

        // 選中的那一件：選過的還在就留著，不在了就落到第一件
        var chosen = events.Where(row => row.id == picked).DefaultIfEmpty(events.FirstOrDefault()).First();

        return new()
        {
            Counts = total == 0
                ? "共 0 筆"
                : $"共 {total} 筆：{string.Join(" · ", counts.Select(row => $"{row.event_name} {row.event_count}"))}",
            Total = total,
            Matched = events.Count,
            Matcheds = Enumerable.Repeat(events.Count, 1).Where(_ => query.Trim().Length > 0).ToList(),
            Clears = Enumerable.Repeat(icons["x"], 1).Where(_ => query.Length > 0).ToList(),
            HasRows = events.Count > 0,

            // 欄名：排序的那一欄帶箭頭、字深一階
            HeadAt = Head("時間", "at", sort, descending),
            HeadType = Head("事件", "type", sort, descending),
            HeadTool = Head("工具", "tool", sort, descending),
            HeadWho = Head("帳號", "who", sort, descending),

            Rows = events
                .Select(row => new RowItem
                {
                    Id = row.id ?? 0,
                    At = $"{row.created_text}",
                    Ago = Ago(row.created_at, now),
                    Event = $"{row.event_name}",
                    EventInk = EventInk(row.event_type),
                    Tool = row.tool_name ?? Blank,
                    Who = row.account ?? Blank,
                    Message = row.message ?? Blank,
                })
                .ToList(),
            Picked = chosen?.id ?? 0,
            Chosen = events.FindIndex(row => row.id == chosen?.id),

            // 有紀錄但這一次沒有一件符合
            Misses = Enumerable.Repeat(query, 1).Where(_ => total > 0 && events.Count == 0).ToList(),

            // 一件都還沒發生過
            Firsts = Enumerable.Repeat(icons["history"], 1).Where(_ => total == 0).ToList(),

            // 右邊：選中那一件的明細
            Picks = new[] { chosen }
                .Where(row => row is not null)
                .Select(row => new PickView
                {
                    Event = $"{row!.event_name}",
                    EventInk = EventInk(row.event_type),
                    At = $"{row.created_text} · {Ago(row.created_at, now)}",
                    Tool = row.tool_name ?? Blank,
                    Who = row.account ?? Blank,
                    Function = row.function_name ?? Blank,
                    Message = row.message ?? Blank,
                    Stacks = new[] { row.stack_trace ?? string.Empty }.Where(one => one.Length > 0).ToList(),
                    CopyLabel = copied ? "已複製" : "複製",
                    CopyInk = copied ? Copied : Ink,
                    CopyPaths = icons[copied ? "check" : "copy"],

                    // 複製的字：時間、事件、工具、帳號、方法、訊息、呼叫堆疊，空的不放
                    CopyText = string.Join("\n", new[] { row.created_text, row.event_name, row.tool_name, row.account, row.function_name, row.message, row.stack_trace }
                        .Where(one => !string.IsNullOrEmpty(one))),
                })
                .ToList(),
        };
    }

    #endregion

    #region 一欄的欄名

    /// <summary>一欄的欄名：排序的那一欄帶箭頭、字深一階</summary>
    /// <param name="text">欄名</param>
    /// <param name="key">這一欄的代碼</param>
    /// <param name="sort">現在照哪一欄排</param>
    /// <param name="descending">由大到小</param>
    private static HeadItem Head(string text, string key, string sort, bool descending) =>
        new()
        {
            Key = key,
            Text = key == sort ? $"{text} {(descending ? "↓" : "↑")}" : text,
            Ink = key == sort ? Ink : Muted,
        };

    #endregion

    #region 幾分前

    /// <summary>那一刻離現在多久：幾秒、幾分、幾時幾分、幾天幾時前</summary>
    /// <param name="at">那一刻（UTC）</param>
    /// <param name="now">現在（UTC）</param>
    public static string Ago(DateTime? at, DateTime now)
    {
        var seconds = (long)Math.Max(0, Math.Round((now - (at ?? now)).TotalSeconds));
        var minutes = seconds / 60;
        var hours = minutes / 60;

        return new[]
        {
            (seconds < 60, $"{seconds} 秒前"),
            (minutes < 60, $"{minutes} 分前"),
            (hours < 24, $"{hours} 時 {minutes % 60} 分前"),
            (true, $"{hours / 24} 天 {hours % 24} 時前"),
        }.First(one => one.Item1).Item2;
    }

    #endregion

    #region 對照

    /// <summary>沒有值的格子</summary>
    private const string Blank = "—";

    /// <summary>一般的字</summary>
    private const string Ink = "#1b1b1b";

    /// <summary>沒選中那幾欄欄名的字</summary>
    private const string Muted = "#5c5c5c";


    /// <summary>複製過了的勾號與字</summary>
    private const string Copied = "#107c10";

    /// <summary>事件名的顏色：錯誤紅字，其餘一般</summary>
    /// <param name="type">event_log.type</param>
    private static string EventInk(string? type) =>
        type == "error" ? "#a4262c" : Ink;


    #endregion

    #region 模型

    /// <summary>日誌這一頁</summary>
    public class LogView
    {
        /// <summary>標題旁的字：共幾筆，每一種幾筆</summary>
        public string Counts { get; set; } = string.Empty;

        /// <summary>全部幾筆</summary>
        public int Total { get; set; }

        /// <summary>符合搜尋字的有幾筆</summary>
        public int Matched { get; set; }

        /// <summary>有搜尋字時「符合幾筆」那一段；沒打字是零筆</summary>
        public List<int> Matcheds { get; set; } = [];

        /// <summary>有打字時搜尋框裡的清除鈕；沒打字是零筆</summary>
        public List<List<string>> Clears { get; set; } = [];

        /// <summary>有符合的紀錄，欄名才出現</summary>
        public bool HasRows { get; set; }

        /// <summary>時間那一欄的欄名</summary>
        public HeadItem HeadAt { get; set; } = new();

        /// <summary>事件那一欄的欄名</summary>
        public HeadItem HeadType { get; set; } = new();

        /// <summary>工具那一欄的欄名</summary>
        public HeadItem HeadTool { get; set; } = new();

        /// <summary>帳號那一欄的欄名</summary>
        public HeadItem HeadWho { get; set; } = new();

        /// <summary>清單擺出來的每一列</summary>
        public List<RowItem> Rows { get; set; } = [];

        /// <summary>選中那一件的流水號；一件都沒有是 0</summary>
        public int Picked { get; set; }

        /// <summary>選中的是清單第幾列；一件都沒有是 -1</summary>
        public int Chosen { get; set; }

        /// <summary>沒有一件符合時那一段的搜尋字；有符合是零筆</summary>
        public List<string> Misses { get; set; } = [];

        /// <summary>一件都還沒發生過時那一段的圖示；有紀錄是零筆</summary>
        public List<List<string>> Firsts { get; set; } = [];

        /// <summary>右邊選中那一件的明細；一件都沒有是零筆</summary>
        public List<PickView> Picks { get; set; } = [];
    }

    /// <summary>一欄的欄名</summary>
    public class HeadItem
    {
        /// <summary>這一欄的代碼</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>欄名，排序的那一欄帶箭頭</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>字的顏色</summary>
        public string Ink { get; set; } = string.Empty;
    }

    /// <summary>清單上的一列：一格一個值</summary>
    public class RowItem
    {
        /// <summary>流水號</summary>
        public int Id { get; set; }

        /// <summary>發生的時刻</summary>
        public string At { get; set; } = string.Empty;

        /// <summary>離現在多久</summary>
        public string Ago { get; set; } = string.Empty;

        /// <summary>事件名</summary>
        public string Event { get; set; } = string.Empty;

        /// <summary>事件名的顏色</summary>
        public string EventInk { get; set; } = string.Empty;

        /// <summary>哪一支工具</summary>
        public string Tool { get; set; } = string.Empty;

        /// <summary>誰</summary>
        public string Who { get; set; } = string.Empty;

        /// <summary>處理的結果</summary>
        public string Message { get; set; } = string.Empty;
    }


    /// <summary>右邊選中那一件的明細</summary>
    public class PickView
    {
        /// <summary>事件名</summary>
        public string Event { get; set; } = string.Empty;

        /// <summary>事件名的顏色</summary>
        public string EventInk { get; set; } = string.Empty;

        /// <summary>發生的時刻 · 離現在多久</summary>
        public string At { get; set; } = string.Empty;

        /// <summary>哪一支工具</summary>
        public string Tool { get; set; } = string.Empty;

        /// <summary>誰</summary>
        public string Who { get; set; } = string.Empty;

        /// <summary>系統由哪一支方法處理</summary>
        public string Function { get; set; } = string.Empty;

        /// <summary>處理的結果</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>有呼叫堆疊才有那一段；沒有是零筆</summary>
        public List<string> Stacks { get; set; } = [];

        /// <summary>複製鈕上的字</summary>
        public string CopyLabel { get; set; } = string.Empty;

        /// <summary>複製鈕的字色</summary>
        public string CopyInk { get; set; } = string.Empty;

        /// <summary>複製鈕的圖示</summary>
        public List<string> CopyPaths { get; set; } = [];

        /// <summary>按複製時放進剪貼簿的字</summary>
        public string CopyText { get; set; } = string.Empty;
    }

    #endregion
}
