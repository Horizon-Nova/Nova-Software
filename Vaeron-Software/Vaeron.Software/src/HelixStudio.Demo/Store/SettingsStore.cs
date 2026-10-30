using Microsoft.EntityFrameworkCore;
using Models.HelixStudio;

namespace HelixStudio.Store;

/// <summary>設定這個主題唯一碰資料庫的型別</summary>
/// <remarks>讀走 vw_settings 一支，寫才碰 setting；每一次呼叫現查。</remarks>
public class SettingsStore
{
    #region 統一的查詢來源

    /// <summary>平台的資料庫</summary>
    private HelixStudioDbContext Db { get; } = new(
        new DbContextOptionsBuilder<HelixStudioDbContext>()
            .UseNpgsql(new SecretStore().Secret("HelixStudio"))
            .Options);

    /// <summary>設定讀得到的每一列：每一項設定與每一支還裝著的工具</summary>
    private IQueryable<vw_setting> ValidRows() =>
        Db.vw_settings.AsNoTracking();

    #endregion

    #region 專用查詢方法

    /// <summary>單一：每一項設定，與代碼完全相同的那一支工具</summary>
    /// <param name="toolNo">那一支工具的代碼；空字串只查到每一項設定</param>
    public List<vw_setting> QuerySettingsItem(string toolNo) =>
        [.. ValidRows().Where(row => row.type == "setting" || row.tool_no == toolNo)];

    /// <summary>列表：每一項設定與每一支還裝著的工具，先後 view 已經排好</summary>
    public List<vw_setting> QuerySettingsItems() =>
        [.. ValidRows()];

    #endregion

    #region 基本 CRUD 操作

    /// <summary>setting 唯一的寫入：編號指得到的列整列換成送來的值（建立時刻不動），編號是零的新增</summary>
    /// <remarks>切開關、選選項、回復預設、回復每一項、補上這台電腦的套件庫資料夾都呼叫這一支。</remarks>
    /// <param name="rows">要寫回去的每一列</param>
    public void InsertSettingList(IReadOnlyList<setting> rows)
    {
        var ids = rows.Select(row => row.id).ToList();
        var held = Db.settings.Where(row => ids.Contains(row.id)).ToList();

        // 指得到的列：送來的值蓋上去，建立時刻不動
        foreach (var pair in held.Join(rows, one => one.id, row => row.id, (one, row) => (Held: one, Sent: row)))
        {
            Db.Entry(pair.Held).CurrentValues.SetValues(pair.Sent);
            Db.Entry(pair.Held).Property(one => one.created_at).IsModified = false;
        }

        // 編號是零的是新的
        Db.settings.AddRange(rows.Where(row => row.id == 0));

        Db.SaveChanges();
    }

    #endregion
}
