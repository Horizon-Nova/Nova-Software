using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Models.HelixStudio;

namespace HelixStudio.Store;

/// <summary>這個軟體唯一碰資料庫與外部檔案的型別</summary>
/// <remarks>讀的是一個畫面一支 vw_，各有列表與單一兩種查詢；寫的是原始表的新增、異動、刪除；圖示是固定的線條，讀本機的 Seed\icons.json。</remarks>
public class ShellStore
{
    #region 統一的查詢來源

    /// <summary>桌面用戶端讀得到的每一列</summary>
    private HelixStudioDbContext Db { get; } = new(
        new DbContextOptionsBuilder<HelixStudioDbContext>()
            .UseNpgsql(new SecretStore().Secret("HelixStudio"))
            .Options);

    #endregion

    #region 專用查詢方法

    /// <summary>這台電腦的資料庫連不連得上：沒有連線資訊、服務沒在執行、被移除都是連不上</summary>
    public bool QueryReachable() =>
        $"{Db.Database.GetConnectionString()}".Length > 0 && Db.Database.CanConnect();

    /// <summary>外殼：shell 那一列與每一支工具</summary>
    public List<vw_shell> QueryShellList() =>
        [.. Db.vw_shells.AsNoTracking()];

    /// <summary>外殼的一支工具：開它要的檔與存著的工具金鑰</summary>
    /// <param name="toolNo">這一支工具的代碼</param>
    public vw_shell? QueryShellItem(string toolNo) =>
        Db.vw_shells.AsNoTracking().FirstOrDefault(one => one.type == "tool" && one.tool_no == toolNo);

    /// <summary>工具目錄：總數、最近搜尋，加上名稱、維護團隊或說明含有搜尋字的工具</summary>
    /// <param name="query">搜尋框打的字</param>
    public List<vw_catalog> QueryCatalogList(string query) =>
        [.. Db.vw_catalogs.AsNoTracking()
            .Where(one => one.type != "tool"
                || query == string.Empty
                || EF.Functions.ILike(one.name!, $"%{query}%")
                || EF.Functions.ILike(one.vendor!, $"%{query}%")
                || EF.Functions.ILike(one.description!, $"%{query}%"))];

    /// <summary>工具目錄的一支工具：右邊那一欄要的 tool 那一列與每一則更新日誌</summary>
    /// <param name="toolNo">這一支工具的代碼</param>
    public List<vw_catalog> QueryCatalogItem(string toolNo) =>
        [.. Db.vw_catalogs.AsNoTracking().Where(one => one.tool_no == toolNo)];

    /// <summary>指令面板：每一支工具與搜尋過的字</summary>
    public List<vw_palette> QueryPaletteList() =>
        [.. Db.vw_palettes.AsNoTracking()];

    /// <summary>設定：每一項設定與每一支工具</summary>
    public List<vw_setting> QuerySettingList() =>
        [.. Db.vw_settings.AsNoTracking()];

    /// <summary>設定裡的一支工具：設定畫面、解鎖分鐘數</summary>
    /// <param name="toolNo">這一支工具的代碼</param>
    public vw_setting? QuerySettingItem(string toolNo) =>
        Db.vw_settings.AsNoTracking().FirstOrDefault(one => one.type == "tool" && one.tool_no == toolNo);

    /// <summary>圖示名稱對到的每一段路徑</summary>
    public Dictionary<string, List<string>> QueryIconList() =>
        JsonSerializer.Deserialize<Dictionary<string, List<string>>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Seed", "icons.json")))!;


    #endregion

    #region 基本 CRUD 操作

    /// <summary>新增一次搜尋：搜了什麼字；同一個字搜過幾次、最近一次由 view 算</summary>
    /// <param name="term">搜尋的那個字</param>
    public void InsertSearch(string term)
    {
        Db.searches.Add(new search { type = "recent", term = term, created_at = DateTime.UtcNow, updated_at = DateTime.UtcNow });

        Db.SaveChanges();
    }

    /// <summary>新增一支工具：這個代碼第一次匯入</summary>
    /// <param name="data">照套件宣告填好的那一列</param>
    public void InsertTool(tool data)
    {
        data.created_at = DateTime.UtcNow;
        data.updated_at = DateTime.UtcNow;

        Db.tools.Add(data);

        Db.SaveChanges();
    }

    /// <summary>新增一把工具金鑰：啟用一支工具時驗過的那一串（tool 的 key 列）</summary>
    /// <param name="toolNo">這一支工具的代碼</param>
    /// <param name="code">驗過的那一串工具金鑰</param>
    public void InsertKey(string toolNo, string code)
    {
        Db.tools.Add(new tool { type = "key", tool_no = toolNo, code = code, created_at = DateTime.UtcNow, updated_at = DateTime.UtcNow });

        Db.SaveChanges();
    }

    /// <summary>新增一件發生過的事：什麼時候、什麼事情、系統由哪一支方法處理、誰（登入 Windows 的帳號）</summary>
    /// <param name="type">activate、open、unlock、error</param>
    /// <param name="toolNo">哪一支工具；不是對著工具發生的就是空的</param>
    /// <param name="function">系統由哪一支方法處理</param>
    /// <param name="message">處理的結果；錯誤時是錯誤訊息</param>
    /// <param name="stack">錯誤時的呼叫堆疊</param>
    public void InsertEventLog(string type, string? toolNo, string function, string? message, string? stack)
    {
        Db.event_logs.Add(new event_log
        {
            type = type,
            tool_no = toolNo,
            account = Environment.UserName,
            function_name = function,
            message = message,
            stack_trace = stack,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow,
        });

        Db.SaveChanges();
    }

    /// <summary>異動一支工具：同一個代碼再匯入一次，換成這一版；第一次匯入的時刻留著</summary>
    /// <param name="data">照套件宣告填好的那一列</param>
    public void UpdateTool(tool data)
    {
        var kept = Db.tools.First(one => one.type == "tool" && one.tool_no == data.tool_no);

        data.id = kept.id;
        data.created_at = kept.created_at;
        data.updated_at = DateTime.UtcNow;

        Db.Entry(kept).CurrentValues.SetValues(data);

        Db.SaveChanges();
    }

    /// <summary>異動一項設定：改成新的值</summary>
    /// <param name="key">這一項的代碼</param>
    /// <param name="value">新的值</param>
    public void UpdateSetting(string key, string value)
    {
        Db.settings
            .Where(one => one.setting_key == key)
            .ExecuteUpdate(set => set.SetProperty(one => one.value, value).SetProperty(one => one.updated_at, DateTime.UtcNow));
    }

    /// <summary>異動每一項設定：都改回資訊部建議的值</summary>
    public void UpdateSettingList()
    {
        Db.settings
            .ExecuteUpdate(set => set.SetProperty(one => one.value, one => one.fallback).SetProperty(one => one.updated_at, DateTime.UtcNow));
    }

    /// <summary>刪除一支工具：拿掉它那一列；工具金鑰與使用紀錄留著</summary>
    /// <param name="toolNo">這一支工具的代碼</param>
    public void DeleteTool(string toolNo)
    {
        Db.tools
            .Where(one => one.type == "tool" && one.tool_no == toolNo)
            .ExecuteDelete();
    }

    #endregion
}
