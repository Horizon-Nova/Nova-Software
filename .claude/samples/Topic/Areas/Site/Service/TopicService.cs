using Web.Areas.Site.Logic;
using Web.Areas.Site.Store;

namespace Web.Areas.Site.Service;

/// <summary>這個主題的串接：查出來的列交給運算，運算的結果寫回或交回控制器</summary>
/// <remarks>不碰資料庫也不碰 HttpContext；一個使用者動作一支方法，步驟依序寫在裡面。</remarks>
public class TopicService(TopicStore store, TimeZoneInfo zone)
{
    #region 項目列表

    /// <summary>打開項目列表：頁面上那一份空白表單；單一查詢不給代碼，查到的只有分類選單的選項</summary>
    /// <param name="accountId">登入的帳號編號</param>
    public TopicLogic.ItemForm LoadItem(int accountId) =>
        TopicLogic.Item(store.QueryItem(accountId, string.Empty));

    /// <summary>要一次列表：他的每一個項目，最急的在最前面</summary>
    /// <param name="accountId">登入的帳號編號</param>
    /// <param name="keyword">以代碼或名稱搜尋；沒填就是全部</param>
    public List<TopicLogic.ItemRow> LoadItems(int accountId, string? keyword) =>
        TopicLogic.Items(store.QueryItems(accountId, keyword));

    #endregion

    #region 基本 CRUD 操作

    /// <summary>存這一份：附件先落地，再把那一列寫回去</summary>
    /// <param name="accountId">登入的帳號編號</param>
    /// <param name="data">表單上每一格的值</param>
    /// <param name="files">這一次附的每一個檔案；沒有就是空的</param>
    public TopicLogic.Answer LoadSubmitted(int accountId, TopicLogic.ItemInput data, IReadOnlyCollection<IFormFile> files)
    {
        // 這一次上傳一個資料夾，檔名照原本的；內容讀進記憶體交給運算
        var folder = Guid.NewGuid().ToString("N");
        var attached = files
            .Select(one =>
            {
                using var buffer = new MemoryStream();
                one.CopyTo(buffer);

                return new TopicLogic.ItemFile { path = $"{folder}/{Path.GetFileName(one.FileName)}", name = Path.GetFileName(one.FileName), size = one.Length, bytes = buffer.ToArray() };
            })
            .ToList();

        // 他這個代碼現在的那一筆，與分類選單的選項
        var rows = store.QueryItem(accountId, (data.code ?? string.Empty).Trim());

        // 照送上來的值算出要寫回去的那一列與回給瀏覽器那一句
        var saved = TopicLogic.Submit(rows, data, attached, accountId, zone, DateTime.UtcNow);

        // 照做了先把附件寫進 Storage，再寫回那一列
        store.InsertFileList(saved.files);
        store.InsertItemManagementList(saved.row);

        return saved.answer;
    }

    /// <summary>移掉一個項目：只有他自己的那一筆移得掉</summary>
    /// <param name="accountId">登入的帳號編號</param>
    /// <param name="code">要移掉的那一個的代碼</param>
    public TopicLogic.Answer LoadRemoved(int accountId, string code)
    {
        // 他這個代碼現在的那一筆；不是他的，讀到的就是零筆
        var rows = store.QueryItem(accountId, code);

        // 照現在的樣子算出要寫回去的那一列與回給瀏覽器那一句
        var removed = TopicLogic.Remove(rows, DateTime.UtcNow);

        // 刪是狀態：寫回去的是移掉的時刻，不是真的刪掉
        store.InsertItemManagementList(removed.row);

        return removed.answer;
    }

    #endregion
}
