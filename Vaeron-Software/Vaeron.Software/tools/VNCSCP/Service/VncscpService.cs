using HelixStudio.Contract;
using VNCSCP.Logic;
using VNCSCP.Store;

namespace VNCSCP.Service;

/// <summary>VNCSCP 的串接：查出來的列交給運算，運算的結果寫回去或交回畫面；送出與測試在這裡連機台</summary>
/// <remarks>不碰畫面；一個使用者動作一支方法，步驟依序寫在裡面。連線字串與保險箱是平台塞給畫面的。</remarks>
public class VncscpService(string connection, ISecrets secrets)
{
    private readonly VncscpStore store = new(connection);

    #region 主畫面

    /// <summary>打開這一頁、寫完之後重讀：上次的來源資料夾、每一台機台、最近送出與測試</summary>
    public VncscpLogic.BoardView LoadBoard() =>
        VncscpLogic.Board(store.QueryVncscpItems());

    /// <summary>勾、搜尋、排序、時間走一格：照現在的狀態算出畫面上的字</summary>
    /// <param name="machines">每一台機台，加上畫面上的勾與正在跑的那一件</param>
    /// <param name="files">來源的每一個檔</param>
    /// <param name="folder">來源資料夾；挑檔案時是空的</param>
    /// <param name="query">搜尋框的字</param>
    /// <param name="sortKey">照哪一欄排</param>
    /// <param name="sortDesc">反過來排</param>
    /// <param name="busy">正在做的事：空的、send、test</param>
    public VncscpLogic.ScreenView LoadScreen(IReadOnlyList<VncscpLogic.MachineState> machines, IReadOnlyList<VncscpLogic.FileItem> files, string folder, string query, string sortKey, bool sortDesc, string busy) =>
        VncscpLogic.Screen(machines, files, folder, query, sortKey, sortDesc, busy, DateTime.UtcNow);

    #endregion

    #region 來源

    /// <summary>打開這一頁、資料夾有變動：重讀來源資料夾裡的每一個檔</summary>
    /// <param name="folder">來源資料夾</param>
    public VncscpLogic.SourceView LoadFolder(string folder) =>
        VncscpLogic.Folder(folder, store.QueryFolderItems(folder), DateTime.UtcNow);

    /// <summary>選資料夾：讀裡面的每一個檔（含子資料夾），記下這個資料夾，下次開起來還是它</summary>
    /// <param name="path">選的資料夾</param>
    public VncscpLogic.SourceView LoadFolderPicked(string path)
    {
        // 讀資料夾裡的每一個檔，算成來源
        var source = VncscpLogic.Folder(path, store.QueryFolderItems(path), DateTime.UtcNow);

        // 記下這個資料夾：source 那一項設定換上新的路徑
        store.InsertVncscpList(VncscpLogic.SourceChanged(store.QueryVncscpItem(0), source.Folder, DateTime.UtcNow));

        return source;
    }

    /// <summary>選檔案：只送挑的那幾個；不記下來</summary>
    /// <param name="paths">挑的每一個檔的完整路徑</param>
    public VncscpLogic.SourceView LoadFilesPicked(IReadOnlyList<string> paths) =>
        VncscpLogic.Picked(store.QueryFileItems(paths), DateTime.UtcNow);

    #endregion

    #region 送出與測試

    /// <summary>送到勾選的機台：送之前重讀來源；每台送到自己的遠端路徑、一台一條連線同時送；沒填路徑的那台直接記下原因；回傳一批結束後重讀的來源與通知</summary>
    /// <remarks>重讀之後一個檔都沒有就不送，交回重讀的來源、沒有通知。</remarks>
    /// <param name="picked">勾的那幾台的編號</param>
    /// <param name="source">畫面上的來源；資料夾是空的（挑檔案）就送這幾個</param>
    /// <param name="begun">每台的那一列寫好、開始連線時叫一次，帶著重讀後的來源與真的要連的每一台</param>
    /// <param name="ended">每一台結束、寫完結果時叫一次</param>
    /// <param name="token">按了取消</param>
    public async Task<VncscpLogic.SentView> LoadSent(IReadOnlyList<int> picked, VncscpLogic.SourceView source,
        Action<VncscpLogic.SourceView, IReadOnlyList<VncscpLogic.Job>> begun, Action<VncscpLogic.Job> ended, CancellationToken token)
    {
        // 送之前重讀一次來源，送的就是現在的檔；挑檔案的照原樣
        var sending = source.Folder.Length > 0 ? VncscpLogic.Folder(source.Folder, store.QueryFolderItems(source.Folder), DateTime.UtcNow) : source;
        if (sending.Files.Count == 0) return new VncscpLogic.SentView { Source = sending };

        // 每台一列：開始的時刻、送到哪裡、送什麼；沒填路徑的那台當場結束
        var start = VncscpLogic.SendStarted(store.QueryVncscpItems(), picked, sending.Files, Guid.NewGuid(), DateTime.UtcNow);
        store.InsertVncscpList(start.Rows);
        begun(sending, start.Jobs);

        // 按取消時還在跑的台數
        var running = start.Jobs.Count;
        var stopped = 0;
        using var hook = token.Register(() => stopped = running);

        // 同時送：一台一條連線；每台結束就寫下結果
        await Task.WhenAll(start.Jobs.Select(async job =>
        {
            try
            {
                using var session = await Rfb.Login(job.Ip, job.Port, secrets.Secret($"machine:{job.MachineId}"), token);
                job.Connected = session.ConnectedMs;
                job.SendAt = DateTime.UtcNow;
                job.Phase = "send";
                await Rfb.Send(session, start.Files, job.Path, sent => Interlocked.Exchange(ref job.Sent, sent), token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                job.Cancelled = DateTime.UtcNow;
                job.Note = VncscpLogic.Stopped(job, job.Cancelled.Value);
            }
            catch (Exception error)
            {
                job.Note = error.Message;
            }

            job.Ended = DateTime.UtcNow;
            store.InsertVncscpList([VncscpLogic.Finished(job, job.Ended)]);
            running--;
            ended(job);
        }));

        // 一批結束：再重讀一次來源；成功的合成一則，失敗的每台一則；按了取消就只說取消了
        return new VncscpLogic.SentView
        {
            Source = sending.Folder.Length > 0 ? VncscpLogic.Folder(sending.Folder, store.QueryFolderItems(sending.Folder), DateTime.UtcNow) : sending,
            Notices = VncscpLogic.Sent(start, stopped),
        };
    }

    /// <summary>測試連線：勾選的每台連一次、登入一次，記下耗時；回傳一批結束時的通知</summary>
    /// <param name="picked">勾的那幾台的編號</param>
    /// <param name="begun">每台的那一列寫好、開始連線時叫一次，帶著每一台</param>
    /// <param name="ended">每一台結束、寫完結果時叫一次</param>
    /// <param name="token">按了取消</param>
    public async Task<List<VncscpLogic.Notice>> LoadTested(IReadOnlyList<int> picked,
        Action<IReadOnlyList<VncscpLogic.Job>> begun, Action<VncscpLogic.Job> ended, CancellationToken token)
    {
        // 每台一列：開始的時刻
        var start = VncscpLogic.TestStarted(store.QueryVncscpItems(), picked, Guid.NewGuid(), DateTime.UtcNow);
        store.InsertVncscpList(start.Rows);
        begun(start.Jobs);

        // 按取消時還在跑的台數
        var running = start.Jobs.Count;
        var stopped = 0;
        using var hook = token.Register(() => stopped = running);

        // 同時連：登入成功就斷開，記下花了幾毫秒
        await Task.WhenAll(start.Jobs.Select(async job =>
        {
            try
            {
                using var session = await Rfb.Login(job.Ip, job.Port, secrets.Secret($"machine:{job.MachineId}"), token);
                job.Connected = session.ConnectedMs;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                job.Cancelled = DateTime.UtcNow;
                job.Note = VncscpLogic.Stopped(job, job.Cancelled.Value);
            }
            catch (Exception error)
            {
                job.Note = error.Message;
            }

            job.Ended = DateTime.UtcNow;
            store.InsertVncscpList([VncscpLogic.Finished(job, job.Ended)]);
            running--;
            ended(job);
        }));

        // 一批結束：連上的合成一則，連不上的每台一則；按了取消就說幾台還沒回應
        return VncscpLogic.Tested(start, stopped);
    }

    #endregion

    #region 基本 CRUD 操作

    /// <summary>加入一台：照填的值算出那一列寫進去，密碼鎖進保險箱；回傳錯在哪一格與那一則</summary>
    /// <param name="data">新增機台那一列打的字</param>
    public VncscpLogic.AddSaved LoadAdded(VncscpLogic.MachineInput data)
    {
        // 每一台現在的樣子：同一個 IP:Port 不能加兩次
        var rows = store.QueryVncscpItems();

        // 照填的值算出要寫進去的那一列；填錯就是零筆
        var saved = VncscpLogic.Added(rows, data, DateTime.UtcNow);

        // 寫進去，密碼照資料庫給的編號鎖進保險箱
        store.InsertVncscpList(saved.Row);
        foreach (var row in saved.Row) secrets.Keep($"machine:{row.id}", $"{data.password}");

        return saved;
    }

    /// <summary>改一台的遠端路徑（Enter 或離開那一格）：換了位置，機台那份與上次送達就是那個位置的；回傳那一則（沒變是空的）</summary>
    /// <param name="id">那一台的編號</param>
    /// <param name="path">新的遠端路徑</param>
    public string LoadPathChanged(int id, string path)
    {
        // 那一台現在的樣子，整列帶過來換上新的路徑；沒變就是零筆
        var written = VncscpLogic.PathChanged(store.QueryVncscpItem(id), id, path, DateTime.UtcNow);
        store.InsertVncscpList(written);

        // 寫完重讀那一台：新位置有沒有送過
        return VncscpLogic.PathMoved(store.QueryVncscpItem(id), id, written);
    }

    #endregion
}
