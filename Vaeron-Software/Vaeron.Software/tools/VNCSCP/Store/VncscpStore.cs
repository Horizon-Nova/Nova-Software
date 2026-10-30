using System.IO;
using Microsoft.EntityFrameworkCore;
using Models.HelixStudio;
using VNCSCP.Logic;

namespace VNCSCP.Store;

/// <summary>VNCSCP 唯一碰資料庫的型別；也讀本機來源的檔</summary>
/// <remarks>讀走 vw_vncscp 一支，寫才碰 vncscp；每一次呼叫開一條連線現查、用完就關。連線字串是平台塞給畫面的 IData.Connection。</remarks>
public class VncscpStore(string connection)
{
    #region 統一的查詢來源

    /// <summary>連平台資料庫：每一次呼叫開一條</summary>
    private HelixStudioDbContext Db() =>
        new(new DbContextOptionsBuilder<HelixStudioDbContext>().UseNpgsql(connection).Options);

    /// <summary>這支工具讀得到的每一列：設定、每一台還在的機台、每一次送出與測試</summary>
    /// <param name="db">這一次呼叫的連線</param>
    private static IQueryable<vw_vncscp> ValidRows(HelixStudioDbContext db) =>
        db.vw_vncscps.AsNoTracking();

    #endregion

    #region 專用查詢方法

    /// <summary>單一：每一項設定，與編號相同的那一台機台</summary>
    /// <param name="id">那一台機台的編號；零只查到每一項設定</param>
    public List<vw_vncscp> QueryVncscpItem(int id)
    {
        using var db = Db();

        return [.. ValidRows(db).Where(row => row.type == "setting" || (row.type == "machine" && row.id == id))];
    }

    /// <summary>列表：每一項設定、每一台機台、每一次送出與測試，先後 view 已經排好</summary>
    public List<vw_vncscp> QueryVncscpItems()
    {
        using var db = Db();

        return [.. ValidRows(db)];
    }

    /// <summary>來源資料夾（含子資料夾）裡的每一個檔；讀不到的子資料夾跳過，資料夾本身不見了就是零筆</summary>
    /// <param name="root">來源資料夾</param>
    public List<VncscpLogic.FileItem> QueryFolderItems(string root)
    {
        try
        {
            return [.. Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 })
                .Select(full => new FileInfo(full))
                .Select(info => new VncscpLogic.FileItem { Full = info.FullName, Bytes = info.Length, Mtime = info.LastWriteTimeUtc })];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>挑的那幾個檔</summary>
    /// <param name="paths">每一個檔的完整路徑</param>
    public List<VncscpLogic.FileItem> QueryFileItems(IReadOnlyList<string> paths) =>
        [.. paths
            .Select(full => new FileInfo(full))
            .Select(info => new VncscpLogic.FileItem { Full = info.FullName, Bytes = info.Length, Mtime = info.LastWriteTimeUtc })];

    #endregion

    #region 基本 CRUD 操作

    /// <summary>vncscp 唯一的寫入：編號指得到的列整列換成送來的值（建立時刻不動），編號是零的新增</summary>
    /// <remarks>寫入方法照資料表開，不照功能開；新增機台、改遠端路徑、記下來源資料夾、送出與測試的開始與結束都呼叫這一支。
    /// 送來的列由 Logic 從現在的樣子整列帶過來，所以沒改的欄位維持原值；新增的那幾列寫完之後帶著資料庫給的編號。</remarks>
    /// <param name="rows">要寫回去的每一列</param>
    public void InsertVncscpList(IReadOnlyList<vncscp> rows)
    {
        using var db = Db();
        var ids = rows.Select(row => row.id).ToList();
        var held = db.vncscps.Where(row => ids.Contains(row.id)).ToList();

        // 指得到的列：送來的值蓋上去，建立時刻不動
        foreach (var pair in held.Join(rows, one => one.id, row => row.id, (one, row) => (Held: one, Sent: row)))
        {
            db.Entry(pair.Held).CurrentValues.SetValues(pair.Sent);
            db.Entry(pair.Held).Property(one => one.created_at).IsModified = false;
        }

        // 編號是零的是新的
        db.vncscps.AddRange(rows.Where(row => row.id == 0));

        db.SaveChanges();
    }

    #endregion
}
