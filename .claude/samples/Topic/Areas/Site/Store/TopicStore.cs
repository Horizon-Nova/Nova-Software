using Models.Site;
using Web.Areas.Site.Logic;

namespace Web.Areas.Site.Store;

/// <summary>這個主題唯一碰資料庫的型別</summary>
/// <remarks>讀走 vw_item 一支，寫才碰 item_management；每一次請求現查。
/// 看得到哪幾列由 vw_item 帶出的 account_id 決定，這裡直接比對登入的人。</remarks>
public class TopicStore(SiteDbContext db, IWebHostEnvironment environment)
{
    /// <summary>附件的資料夾；啟動後不變</summary>
    private readonly string folder = Path.Combine(environment.ContentRootPath, "Storage", "item");

    #region 統一的查詢來源

    /// <summary>登入的人讀得到的每一列：他自己的項目，與每個人都看得到的分類選項</summary>
    /// <param name="accountId">登入的帳號編號</param>
    private IQueryable<vw_item> ValidRows(int accountId) =>
        db.vw_items.Where(row => row.account_id == null || row.account_id == accountId);

    #endregion

    #region 專用查詢方法

    /// <summary>單一：代碼完全相同的那一筆，與分類選單的每一個選項</summary>
    /// <param name="accountId">登入的帳號編號</param>
    /// <param name="code">那一筆的代碼；空字串只查到分類選項</param>
    public List<vw_item> QueryItem(int accountId, string code) =>
        [.. ValidRows(accountId)
            .Where(row => row.type != "item" || row.code == code)];

    /// <summary>列表：他的每一筆（給了字就只留代碼或名稱裡有那個字的），與分類選單的每一個選項</summary>
    /// <param name="accountId">登入的帳號編號</param>
    /// <param name="keyword">以代碼或名稱搜尋；沒填就是全部</param>
    public List<vw_item> QueryItems(int accountId, string? keyword) =>
        [.. ValidRows(accountId)
            .Where(row => row.type != "item" || string.IsNullOrEmpty(keyword) || row.code!.Contains(keyword) || row.name!.Contains(keyword))];

    #endregion

    #region 基本 CRUD 操作

    /// <summary>item_management 唯一的寫入：編號指得到的列整列換成送來的值（建立時刻不動），編號是零的新增</summary>
    /// <remarks>寫入方法照資料表開，不照功能開；存與移都呼叫這一支。送來的列由 Logic 從現在的樣子整列帶過來，所以沒改的欄位維持原值。</remarks>
    /// <param name="rows">要寫回去的每一列</param>
    public void InsertItemManagementList(IReadOnlyList<item_management> rows)
    {
        var ids = rows.Select(row => row.id).ToList();
        var held = db.item_managements.Where(row => ids.Contains(row.id)).ToList();

        // 指得到的列：送來的值蓋上去，建立時刻不動
        foreach (var pair in held.Join(rows, one => one.id, row => row.id, (one, row) => (Held: one, Sent: row)))
        {
            db.Entry(pair.Held).CurrentValues.SetValues(pair.Sent);
            db.Entry(pair.Held).Property(one => one.created_at).IsModified = false;
        }

        // 編號是零的是新的
        db.item_managements.AddRange(rows.Where(row => row.id == 0));

        db.SaveChanges();
    }

    /// <summary>附件落地：寫到 Storage/item/{相對位置}</summary>
    /// <param name="files">這一次附的每一個檔案</param>
    public void InsertFileList(IReadOnlyList<TopicLogic.ItemFile> files)
    {
        foreach (var one in files)
        {
            var target = Path.Combine(folder, one.path);

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, one.bytes);
        }
    }

    #endregion
}
