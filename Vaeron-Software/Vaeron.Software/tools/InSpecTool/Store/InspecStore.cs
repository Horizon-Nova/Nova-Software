using System.IO;
using System.Text;
using InSpecTool.Logic;
using Microsoft.EntityFrameworkCore;
using Models.HelixStudio;

namespace InSpecTool.Store;

/// <summary>InSpecTool 唯一碰資料庫的型別</summary>
/// <remarks>量測頁讀 vw_inspec_measure、設定讀 vw_inspec_setup，寫才碰 inspec；每一次呼叫現查。連線是平台塞給這支工具的 IData.Connection。</remarks>
/// <param name="connection">平台資料庫的連線</param>
public class InspecStore(string connection)
{
    #region 統一的查詢來源

    /// <summary>平台的資料庫</summary>
    private HelixStudioDbContext Db { get; } = new(
        new DbContextOptionsBuilder<HelixStudioDbContext>()
            .UseNpgsql(connection)
            .Options);

    /// <summary>量測頁讀得到的每一列：每一項設定、每一個還在的欄位、每一次執行</summary>
    private IQueryable<vw_inspec_measure> MeasureRows() =>
        Db.vw_inspec_measures.AsNoTracking();

    /// <summary>設定讀得到的每一列：每一項設定、每一個還在的欄位</summary>
    private IQueryable<vw_inspec_setup> SetupRows() =>
        Db.vw_inspec_setups.AsNoTracking();

    #endregion

    #region 專用查詢方法

    /// <summary>單一：每一項設定、每一個還在的欄位，與編號是這個的那一次執行</summary>
    /// <param name="id">那一次執行在 inspec 的編號；零只查到設定與欄位</param>
    public List<vw_inspec_measure> QueryMeasureItem(int id) =>
        [.. MeasureRows().Where(row => row.type != "run" || row.id == id)];

    /// <summary>列表：每一項設定、每一個還在的欄位與每一次執行，先後 view 已經排好</summary>
    public List<vw_inspec_measure> QueryMeasureItems() =>
        [.. MeasureRows()];

    /// <summary>單一：代碼完全相同的那一項設定，與每一個還在的欄位</summary>
    /// <param name="key">那一項設定的代碼；空字串只查到欄位</param>
    public List<vw_inspec_setup> QuerySetupItem(string key) =>
        [.. SetupRows().Where(row => row.type == "field" || row.setting_key == key)];

    /// <summary>列表：每一項設定與每一個還在的欄位，先後 view 已經排好</summary>
    public List<vw_inspec_setup> QuerySetupItems() =>
        [.. SetupRows()];

    #endregion

    #region 基本 CRUD 操作

    /// <summary>inspec 唯一的寫入：編號指得到的列整列換成送來的值（建立時刻不動），編號是零的新增</summary>
    /// <remarks>改設定、存欄位結構、記一次執行、跑完記結果、歸檔都呼叫這一支。送來的列由 Logic 整列帶過來，所以沒改的欄位維持原值；新增的列寫完就拿到編號。</remarks>
    /// <param name="rows">要寫回去的每一列</param>
    public void InsertInspecList(IReadOnlyList<inspec> rows)
    {
        var ids = rows.Select(row => row.id).ToList();
        var held = Db.inspecs.Where(row => ids.Contains(row.id)).ToList();

        // 指得到的列：送來的值蓋上去，建立時刻不動
        foreach (var pair in held.Join(rows, one => one.id, row => row.id, (one, row) => (Held: one, Sent: row)))
        {
            Db.Entry(pair.Held).CurrentValues.SetValues(pair.Sent);
            Db.Entry(pair.Held).Property(one => one.created_at).IsModified = false;
        }

        // 編號是零的是新的
        Db.inspecs.AddRange(rows.Where(row => row.id == 0));

        Db.SaveChanges();
    }

    /// <summary>歸檔檔落地：每一筆內容附加到它的歸檔檔，新檔寫 UTF-16LE 加 BOM；寫不進去不吞掉，交給全域的錯誤捕捉記進 event_log</summary>
    /// <param name="files">這一次要附加的每一筆</param>
    public void InsertArchiveList(IReadOnlyList<InspecLogic.ArchiveFile> files)
    {
        foreach (var one in files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(one.Path)!);

            var fresh = !File.Exists(one.Path) || new FileInfo(one.Path).Length == 0;
            using var stream = new FileStream(one.Path, FileMode.Append, FileAccess.Write, FileShare.Read);
            var bytes = (fresh ? Encoding.Unicode.GetPreamble() : []).Concat(Encoding.Unicode.GetBytes(one.Content)).ToArray();

            stream.Write(bytes, 0, bytes.Length);
        }
    }

    #endregion
}
