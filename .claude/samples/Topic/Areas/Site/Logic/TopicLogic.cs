using System.ComponentModel.DataAnnotations;
using Models.Site;

namespace Web.Areas.Site.Logic;

/// <summary>這個主題的純運算</summary>
/// <remarks>不碰資料庫也不碰這一次請求，給什麼算什麼；沒有狀態，所以是靜態的。
/// 離期限幾小時、排第幾、先後、顯示的字都是 vw_item 算好的，這裡只把列攤成畫面要的形狀，與算出要寫回去的那一列。</remarks>
public static class TopicLogic
{
    #region 項目列表

    /// <summary>打開項目列表：頁面上那一份空白表單的分類選單；選項的先後 vw_item 已經排好</summary>
    /// <param name="rows">vw_item 查到的列；kind 列是分類選單上的一個選項</param>
    public static ItemForm Item(IReadOnlyList<vw_item> rows) =>
        new()
        {
            kinds = [.. rows
                .Where(row => row.type == "kind")
                .Select(row => new ItemOption { code = row.code!, group = row.group_name!, name = row.name! })],
        };

    /// <summary>要一次列表：他的每一個項目，最急的在最前面</summary>
    /// <param name="rows">vw_item 查到的列；item 列是一個項目</param>
    public static List<ItemRow> Items(IReadOnlyList<vw_item> rows) =>
        [.. rows
            .Where(row => row.type == "item")
            .Select(row => new ItemRow
            {
                code = row.code!,
                name = row.name!,
                kind_name = row.kind_name!,
                due_text = row.due_text!,
                remaining_hours = row.remaining_hours!.Value,
                rank = row.rank!.Value,
            })];

    #endregion

    #region 基本 CRUD 操作

    /// <summary>存這一份：同一個人同一個代碼已經有了就改那一筆，沒有就新增；算出要寫回去的那一列、要落地的附件，與回給瀏覽器那一句</summary>
    /// <remarks>寫不成的原因先收齊，有任何一個就不寫，只把原因回去。改的那一筆從它現在的樣子整列帶過來，沒送上來的欄位維持原值。
    /// 表單上留白的格子送上來是 null（MVC 把空字串當成沒有值），所以輸入模型的每一格都可以是 null。</remarks>
    /// <param name="rows">vw_item 查到的：這個人這個代碼的那一筆（還沒有就是零筆），與分類選單的每一個選項</param>
    /// <param name="data">表單上每一格的值</param>
    /// <param name="files">這一次附的每一個檔案，相對位置已經取好</param>
    /// <param name="accountId">登入的帳號編號，新增的那一筆屬於他</param>
    /// <param name="zone">站台的時區，表單上的期限照它換成 UTC</param>
    /// <param name="now">這一次請求的時刻</param>
    public static ItemSaved Submit(IReadOnlyList<vw_item> rows, ItemInput data, IReadOnlyList<ItemFile> files, int accountId, TimeZoneInfo zone, DateTime now)
    {
        // 這個人這個代碼現在的那一筆
        var held = rows.Where(row => row.type == "item").ToList();

        // 寫不成的原因
        var faults = new[]
            {
                (Fault: string.IsNullOrWhiteSpace(data.code), Text: "代碼要填"),
                (Fault: string.IsNullOrWhiteSpace(data.name), Text: "名稱要填"),
                (Fault: data.due_at == null, Text: "期限要填"),
                (Fault: !string.IsNullOrEmpty(data.kind) && !rows.Any(row => row.type == "kind" && row.code == data.kind), Text: "分類要從選單上挑"),
            }
            .Where(one => one.Fault)
            .Select(one => one.Text)
            .ToList();

        // 照做的那一份；有任何原因就沒有
        var chosen = new[] { data }.Where(_ => faults.Count == 0).ToList();

        return new ItemSaved
        {
            files = [.. files.Where(_ => chosen.Count > 0)],
            row = [.. chosen.Select(one => new item_management
            {
                id = held.Select(row => row.id!.Value).DefaultIfEmpty(0).First(),
                type = "item",
                owner_id = accountId,
                code = one.code!.Trim(),
                name = one.name!.Trim(),
                kind = one.kind,
                due_at = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(one.due_at!.Value, DateTimeKind.Unspecified), zone),
                file_paths = [.. held.SelectMany(row => row.file_paths!).Concat(files.Select(file => file.path))],
                updated_at = now,
            })],
            answer = faults
                .Take(1)
                .Select(_ => new Answer { kind = "error", message = string.Join("；", faults) })
                .DefaultIfEmpty(new Answer { kind = "success", message = "已儲存" })
                .First(),
        };
    }

    /// <summary>移掉一個項目：寫的是移掉的時刻，不是真的刪掉；那一筆從它現在的樣子整列帶過來，其他欄位維持原值</summary>
    /// <param name="rows">vw_item 查到的：這個人這個代碼的那一筆，與分類選單的每一個選項</param>
    /// <param name="now">這一次請求的時刻</param>
    public static ItemSaved Remove(IReadOnlyList<vw_item> rows, DateTime now)
    {
        // 這個人這個代碼現在的那一筆；已經移掉的不在 vw_item 裡
        var held = rows.Where(row => row.type == "item").ToList();

        // 移不掉的原因
        var faults = new[]
            {
                (Fault: held.Count == 0, Text: "找不到這一個項目，可能已經移掉了，請重新查詢"),
            }
            .Where(one => one.Fault)
            .Select(one => one.Text)
            .ToList();

        return new ItemSaved
        {
            row = [.. held
                .Where(_ => faults.Count == 0)
                .Select(row => new item_management
                {
                    id = row.id!.Value,
                    type = "item",
                    owner_id = row.account_id,
                    code = row.code!,
                    name = row.name!,
                    kind = row.kind,
                    due_at = row.due_at,
                    file_paths = [.. row.file_paths!],
                    removed_at = now,
                    updated_at = now,
                })],
            answer = faults
                .Take(1)
                .Select(_ => new Answer { kind = "error", message = string.Join("；", faults) })
                .DefaultIfEmpty(new Answer { kind = "success", message = "已移掉" })
                .First(),
        };
    }

    #endregion

    #region 模型

    /// <summary>頁面上那一份表單要用的選項</summary>
    public class ItemForm
    {
        /// <summary>分類選單上的每一個選項，先後照 vw_item</summary>
        public List<ItemOption> kinds { get; set; } = [];
    }

    /// <summary>選單上的一個選項</summary>
    public class ItemOption
    {
        /// <summary>選項的值；長度照資料表欄位的定義</summary>
        [StringLength(20)]
        public string code { get; set; } = string.Empty;

        /// <summary>它屬於哪一個大類；Razor 照這一欄畫成 optgroup</summary>
        [StringLength(50)]
        public string group { get; set; } = string.Empty;

        /// <summary>選項寫在畫面上的字</summary>
        [StringLength(100)]
        public string name { get; set; } = string.Empty;
    }

    /// <summary>項目列表上的一列</summary>
    public class ItemRow
    {
        /// <summary>項目的代碼；長度照資料表欄位的定義</summary>
        [StringLength(20)]
        public string code { get; set; } = string.Empty;

        /// <summary>項目的名稱；長度照資料表欄位的定義</summary>
        [StringLength(100)]
        public string name { get; set; } = string.Empty;

        /// <summary>分類寫在畫面上的字；沒選分類是破折號</summary>
        [StringLength(100)]
        public string kind_name { get; set; } = string.Empty;

        /// <summary>期限，vw_item 照站台時區寫好的字</summary>
        [StringLength(16)]
        public string due_text { get; set; } = string.Empty;

        /// <summary>離期限還有幾小時；過了期限是負值</summary>
        public decimal remaining_hours { get; set; }

        /// <summary>照期限由近到遠排第幾，從 1 起算</summary>
        public int rank { get; set; }
    }

    /// <summary>表單送上來的那一份；留白的格子是 null</summary>
    public class ItemInput
    {
        /// <summary>項目的代碼；長度照資料表欄位的定義</summary>
        [StringLength(20)]
        public string? code { get; set; }

        /// <summary>項目的名稱；長度照資料表欄位的定義</summary>
        [StringLength(100)]
        public string? name { get; set; }

        /// <summary>期限，站台時區的時間</summary>
        public DateTime? due_at { get; set; }

        /// <summary>分類代碼；選「不適用」是 null</summary>
        [StringLength(20)]
        public string? kind { get; set; }
    }

    /// <summary>這一次附上來、要寫進 Storage 的一個檔案</summary>
    public class ItemFile
    {
        /// <summary>在 Storage/item/ 底下的相對位置：一次上傳一個資料夾，檔名照原本的</summary>
        public string path { get; set; } = string.Empty;

        /// <summary>上傳時的檔名</summary>
        public string name { get; set; } = string.Empty;

        /// <summary>位元組數</summary>
        public long size { get; set; }

        /// <summary>檔案內容</summary>
        public byte[] bytes { get; set; } = [];
    }

    /// <summary>存或移之後要寫回去的那一列、要落地的附件，與回給瀏覽器那一句</summary>
    public class ItemSaved
    {
        /// <summary>要寫進 Storage 的附件；寫不成時是空的</summary>
        public List<ItemFile> files { get; set; } = [];

        /// <summary>要寫回 item_management 的那一列；寫不成時是零筆</summary>
        public List<item_management> row { get; set; } = [];

        /// <summary>回給瀏覽器那一句</summary>
        public Answer answer { get; set; } = new();
    }

    /// <summary>回給瀏覽器的那一句：寫入動作都回這個形狀</summary>
    public class Answer
    {
        /// <summary>showToast 認得的那幾種：success／error／warning／info，它決定提示的樣式</summary>
        public string kind { get; set; } = string.Empty;

        /// <summary>提示寫什麼</summary>
        public string message { get; set; } = string.Empty;
    }

    #endregion
}
