using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Models.HelixStudio;

namespace HelixStudio.Store;

/// <summary>這個軟體唯一碰資料庫與外部檔案的型別</summary>
/// <remarks>讀走 vw_shell；圖示是固定的線條，讀本機的 Seed\icons.json。</remarks>
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

    /// <summary>工具、這台裝置與用它的人，每一列</summary>
    public List<vw_shell> QueryShells() =>
        [.. Db.vw_shells];

    /// <summary>一支工具那一列</summary>
    /// <param name="toolNo">這一支工具的代碼</param>
    public vw_shell QueryShell(string toolNo) =>
        Db.vw_shells.Single(one => one.tool_no == toolNo);

    /// <summary>設定的每一項</summary>
    public List<vw_setting> QuerySettings() =>
        [.. Db.vw_settings];

    /// <summary>問答與快速提問的每一則</summary>
    public List<vw_assist> QueryAssists() =>
        [.. Db.vw_assists];

    /// <summary>圖示名稱對到的每一段路徑</summary>
    public Dictionary<string, List<string>> QueryIcons() =>
        JsonSerializer.Deserialize<Dictionary<string, List<string>>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Seed", "icons.json")))!;


    /// <summary>把搜尋過的字記起來：記過的就更新它最近用到的時刻</summary>
    /// <param name="term">搜尋的那個字</param>
    public void SaveSearch(string term)
    {
        var rows = Db.search_managements.Where(one => one.term == term).ToList();

        rows.ForEach(one => one.used_at = DateTime.UtcNow);
        Db.search_managements.AddRange(Enumerable
            .Repeat(new search_management { term = term, used_at = DateTime.UtcNow }, 1)
            .Where(_ => rows.Count == 0));

        Db.SaveChanges();
    }

    /// <summary>把一項設定改成新的值</summary>
    /// <param name="key">這一項的代碼</param>
    /// <param name="value">新的值</param>
    public void SaveSetting(string key, string value)
    {
        var rows = Db.setting_managements.Where(one => one.setting_key == key).ToList();

        rows.ForEach(one => one.value = value);

        Db.SaveChanges();
    }

    /// <summary>把每一項設定都改回資訊部建議的值</summary>
    public void ResetSettings()
    {
        var rows = Db.setting_managements.ToList();

        rows.ForEach(one => one.value = one.fallback);

        Db.SaveChanges();
    }

    /// <summary>把說過的話寫進去</summary>
    /// <param name="who">誰說的</param>
    /// <param name="text">說了什麼</param>
    /// <param name="cite">附的來源</param>
    public void SaveMessage(string who, string text, string cite)
    {
        Db.message_managements.Add(new message_management { type = "message", who = who, text = text, cite = cite });

        Db.SaveChanges();
    }

    /// <summary>把這台裝置上的問答紀錄清掉</summary>
    public void DropMessages()
    {
        Db.message_managements.RemoveRange(Db.message_managements.Where(one => one.type == "message"));

        Db.SaveChanges();
    }

    /// <summary>一支工具裝完、更新完或移除完之後，把新的狀態與裝著的版本寫回去</summary>
    /// <param name="toolNo">這一支工具的代碼</param>
    /// <param name="state">新的狀態</param>
    /// <param name="version">裝著的是哪一版</param>
    public void SaveTool(string toolNo, string state, string version)
    {
        var row = Db.tool_managements.Single(one => one.tool_no == toolNo);

        row.state = state;
        row.installed_version = version;

        Db.SaveChanges();
    }

    #endregion
}
