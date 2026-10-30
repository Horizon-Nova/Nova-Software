using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Models.HelixStudio;

namespace HelixStudio.Store;

/// <summary>外殼這個主題唯一碰資料庫與外部檔案的型別</summary>
/// <remarks>讀走 vw_shell 一支，寫才碰 tool；每一次呼叫現查。圖示是固定的線條，讀本機的 Seed\icons.json。</remarks>
public class ShellStore
{
    #region 統一的查詢來源

    /// <summary>平台的資料庫</summary>
    private HelixStudioDbContext Db { get; } = new(
        new DbContextOptionsBuilder<HelixStudioDbContext>()
            .UseNpgsql(new SecretStore().Secret("HelixStudio"))
            .Options);

    /// <summary>外殼讀得到的每一列：shell 那一列與每一支還裝著的工具</summary>
    private IQueryable<vw_shell> ValidRows() =>
        Db.vw_shells.AsNoTracking();

    #endregion

    #region 專用查詢方法

    /// <summary>這台電腦的資料庫連不連得上：沒有連線資訊、服務沒在執行、被移除都是連不上</summary>
    public bool QueryReachable() =>
        $"{Db.Database.GetConnectionString()}".Length > 0 && Db.Database.CanConnect();

    /// <summary>單一：shell 那一列與代碼完全相同的那一支工具</summary>
    /// <param name="toolNo">那一支工具的代碼；空字串只查到 shell 那一列</param>
    public List<vw_shell> QueryShellItem(string toolNo) =>
        [.. ValidRows().Where(row => row.type == "shell" || row.tool_no == toolNo)];

    /// <summary>列表：shell 那一列與每一支還裝著的工具，側欄的先後 view 已經排好</summary>
    public List<vw_shell> QueryShellItems() =>
        [.. ValidRows()];

    /// <summary>圖示名稱對到的每一段路徑</summary>
    public Dictionary<string, List<string>> QueryIconItems() =>
        JsonSerializer.Deserialize<Dictionary<string, List<string>>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Seed", "icons.json")))!;

    #endregion

    #region 基本 CRUD 操作

    /// <summary>tool 唯一的寫入：編號指得到的列整列換成送來的值（建立時刻不動），編號是零的新增</summary>
    /// <remarks>寫入方法照資料表開，不照功能開；匯入、移除、啟用、切自動啟動都呼叫這一支。送來的列由 Logic 從現在的樣子整列帶過來。</remarks>
    /// <param name="rows">要寫回去的每一列</param>
    public void InsertToolList(IReadOnlyList<tool> rows)
    {
        var ids = rows.Select(row => row.id).ToList();
        var held = Db.tools.Where(row => ids.Contains(row.id)).ToList();

        // 指得到的列：送來的值蓋上去，建立時刻不動
        foreach (var pair in held.Join(rows, one => one.id, row => row.id, (one, row) => (Held: one, Sent: row)))
        {
            Db.Entry(pair.Held).CurrentValues.SetValues(pair.Sent);
            Db.Entry(pair.Held).Property(one => one.created_at).IsModified = false;
        }

        // 編號是零的是新的
        Db.tools.AddRange(rows.Where(row => row.id == 0));

        Db.SaveChanges();
    }

    #endregion
}
