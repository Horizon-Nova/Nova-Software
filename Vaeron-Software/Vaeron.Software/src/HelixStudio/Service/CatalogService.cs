using HelixStudio.Logic;
using HelixStudio.Store;
using Models.HelixStudio;

namespace HelixStudio.Service;

/// <summary>工具目錄這個主題的串接：查出來的列交給運算，運算的結果交回畫面或寫回去</summary>
/// <remarks>不碰畫面；一個使用者動作一支方法，步驟依序寫在裡面。</remarks>
public class CatalogService
{
    private readonly CatalogStore catalog = new();

    private readonly ShellStore shell = new();

    private readonly PackageStore packages = new();

    /// <summary>圖示的線條：啟動時讀一次</summary>
    private readonly Dictionary<string, List<string>> icons = new ShellStore().QueryIconItems();

    #region 工具目錄

    /// <summary>打開工具目錄、打字搜尋、選一支：總數、最近搜尋，與名稱、維護團隊或說明有那個字的每一支工具</summary>
    /// <param name="keyword">搜尋框打的字</param>
    /// <param name="loaded">資料讀回來了沒有</param>
    /// <param name="slow">讀太久，先擺骨架</param>
    /// <param name="picked">選中的工具代碼</param>
    public CatalogLogic.CatalogView LoadCatalog(string keyword, bool loaded, bool slow, string picked) =>
        CatalogLogic.Catalog(loaded ? catalog.QueryCatalogItems(keyword) : [], icons, keyword, loaded, slow, picked);

    /// <summary>看一支工具的資訊、換分頁、放大截圖：這一支已經不在就是 null</summary>
    /// <param name="toolNo">那一支工具的代碼</param>
    /// <param name="selected">看的是第幾個分頁</param>
    /// <param name="zoom">放大看的那一張截圖；沒有放大是空字串</param>
    public DetailLogic.DetailView? LoadDetail(string toolNo, int selected, string zoom)
    {
        var rows = catalog.QueryCatalogItem(toolNo);

        return rows.Any(row => row.type == "tool") ? DetailLogic.Detail(rows, icons, selected, zoom) : null;
    }

    #endregion

    #region 基本 CRUD 操作

    /// <summary>點最近搜尋的一項、在指令面板選一支工具：這個字記一次</summary>
    /// <param name="terms">記下來的每一個字</param>
    public void LoadSearched(IReadOnlyList<string> terms) =>
        catalog.InsertSearchList([.. terms.Select(one => new search { type = "recent", term = one, created_at = DateTime.UtcNow, updated_at = DateTime.UtcNow })]);

    /// <summary>移除一支工具（名稱打對了）：記一次移除，檔案從這台電腦刪掉；這一次開過的組件 Windows 鎖著，留著並說出在哪裡</summary>
    /// <param name="toolNo">那一支工具的代碼</param>
    /// <returns>回給操作員的那一句</returns>
    public string LoadRemoved(string toolNo)
    {
        // 這一支現在的樣子：名稱與裝在哪裡
        var tool = catalog.QueryCatalogItem(toolNo).First(row => row.type == "tool");

        // 記一次移除：之後工具目錄、側欄、狀態列都不再有它
        shell.InsertToolList([new tool { type = "remove", tool_no = toolNo, capabilities = [], permissions = [], created_at = DateTime.UtcNow, updated_at = DateTime.UtcNow }]);

        // 檔案：這支工具的資料夾（每一版一層）整個刪掉；還映射在平台這個行程裡就留著
        var left = packages.DeleteHome($"{tool.home}");

        return left.Length == 0 ? $"已移除「{tool.name}」" : $"已移除「{tool.name}」；檔案還在使用中，關掉平台後可以手動刪掉 {left}";
    }

    /// <summary>切自動啟動：記一次切換；回給操作員的那一句</summary>
    /// <param name="toolNo">那一支工具的代碼</param>
    /// <param name="on">切成自動啟動（true）或取消（false）</param>
    public string LoadAutostart(string toolNo, bool on)
    {
        var name = catalog.QueryCatalogItem(toolNo).Where(row => row.type == "tool").Select(row => row.name).FirstOrDefault() ?? toolNo;

        shell.InsertToolList([new tool { type = "start", tool_no = toolNo, capabilities = [], permissions = [], autostart = on, created_at = DateTime.UtcNow, updated_at = DateTime.UtcNow }]);

        return on ? $"{name} 已在背景執行；之後 HelixStudio 啟動時自動啟動" : $"{name} 已取消自動啟動；目前執行中的會持續到 HelixStudio 關閉";
    }

    #endregion
}
