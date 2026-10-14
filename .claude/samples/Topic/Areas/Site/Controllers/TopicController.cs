using Microsoft.AspNetCore.Mvc;
using Web.Areas.Site.Core;
using Web.Areas.Site.Logic;
using Web.Areas.Site.Service;

namespace Web.Areas.Site.Controllers;

/// <summary>這個主題的每一個請求</summary>
/// <remarks>
/// 區域與登入檢查在 BaseController 身上；位址由慣例路由決定。
/// 只有打開頁面用 GET；帶資料回來的（列表那一段畫面）與寫入的都是 POST，每一個 POST 都驗防偽權杖。
/// 使用者做得了幾個動作，這裡就有幾支；每一支一組輸入、一個輸出，寫入動作一律回 TopicLogic.Answer。
/// </remarks>
public class TopicController(TopicService sev) : BaseController
{
    #region 項目列表

    /// <summary>打開項目列表：頁面只有外框與空白表單，列表由畫面自己再要一次</summary>
    public IActionResult Items()
    {
        var model = sev.LoadItem(AccountId);

        return View(model);
    }

    /// <summary>要一次列表：回的是 _List 那一段標記，畫面塞進容器</summary>
    /// <param name="keyword">以代碼或名稱搜尋；沒填就是全部</param>
    [HttpPost]
    public IActionResult Rows(string? keyword)
    {
        var model = sev.LoadItems(AccountId, keyword);

        return PartialView("Partials/Items/_List", model);
    }

    #endregion

    #region 基本 CRUD 操作

    /// <summary>存這一份：表單與附件一起用 FormData 送上來，沒有附件時 files 是空的，伺服器只有這一條路</summary>
    /// <param name="data">表單上每一格的值</param>
    /// <param name="files">這一次附的每一個檔案；沒有就是空的</param>
    [HttpPost]
    public IActionResult Submit(TopicLogic.ItemInput data, List<IFormFile> files) =>
        Json(sev.LoadSubmitted(AccountId, data, files));

    /// <summary>移掉一個項目</summary>
    /// <param name="code">要移掉的那一個的代碼</param>
    [HttpPost]
    public IActionResult Remove(string code) =>
        Json(sev.LoadRemoved(AccountId, code));

    #endregion
}
