using HelixStudio.Logic;
using HelixStudio.Store;
using Models.HelixStudio;

namespace HelixStudio.Service;

/// <summary>日誌這個主題的串接：查出來的列交給運算，運算的結果交回畫面或寫回去</summary>
/// <remarks>不碰畫面；一個使用者動作一支方法，步驟依序寫在裡面。</remarks>
public class LogService
{
    private readonly LogStore log = new();

    /// <summary>圖示的線條：啟動時讀一次</summary>
    private readonly Dictionary<string, List<string>> icons = new ShellStore().QueryIconItems();

    #region 日誌

    /// <summary>打開日誌、搜尋、點欄名排序、選一件、複製：照現在的搜尋字查一次，攤成這一頁</summary>
    /// <param name="keyword">搜尋框打的字</param>
    /// <param name="names">事件代碼對到的事件名（日誌頁 XAML 裡的字）</param>
    /// <param name="sort">照哪一欄排：at、type、tool、who</param>
    /// <param name="descending">由大到小</param>
    /// <param name="picked">選中那一件的流水號；還沒選過是 0</param>
    /// <param name="copied">選中那一件剛被複製</param>
    public LogLogic.LogView LoadLog(string keyword, IReadOnlyDictionary<string, string> names, string sort, bool descending, int picked, bool copied) =>
        LogLogic.Log(log.QueryEventLogItems(keyword), icons, names, keyword, sort, descending, picked, copied, DateTime.UtcNow);

    #endregion

    #region 基本 CRUD 操作

    /// <summary>沒人接住的錯誤：記一筆 error，什麼時候、哪一支方法、訊息、呼叫堆疊、誰</summary>
    /// <param name="error">那一個錯誤</param>
    public void LoadFailed(Exception error)
    {
        var site = error.TargetSite;

        log.InsertEventLogList([new event_log
        {
            type = "error",
            account = Environment.UserName,
            function_name = $"{site?.DeclaringType?.FullName}.{site?.Name}",
            message = error.Message,
            stack_trace = error.StackTrace,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow,
        }]);
    }

    #endregion
}
