using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Models.HelixStudio;

namespace VNCSCP.Logic;

/// <summary>VNCSCP 的純運算</summary>
/// <remarks>
/// 不碰資料庫、不碰畫面、不連機台，給什麼算什麼；沒有狀態，所以是靜態的。
/// 機台那份、上次送達、上次回應、最近送出的先後與耗時的字是 vw_vncscp 算好的；這裡把列攤成畫面要的形狀、算出要寫回去的列，與跟著本機來源和時間走的字。
/// 「送出」「測試連線」「N 個檔 · X KB → 路徑」這類固定的字寫在 Board.xaml；紅字照 note 與 cancelled 的值在 XAML 決定。
/// </remarks>
public static class VncscpLogic
{
    #region 主畫面

    /// <summary>讀主畫面：上次的來源資料夾、每一台機台、最近送出與測試（新的在前，view 已經排好）</summary>
    /// <param name="rows">vw_vncscp 查到的每一列</param>
    public static BoardView Board(IReadOnlyList<vw_vncscp> rows) =>
        new()
        {
            // setting 列：上次選的來源資料夾
            Source = rows.Where(row => row.type == "setting").Select(row => $"{row.source}").DefaultIfEmpty(string.Empty).First(),

            // machine 列：一台一列；名稱空的寫 —，稱呼空的寫 IP:Port
            Machines = [.. rows
                .Where(row => row.type == "machine")
                .Select(row => new MachineRow
                {
                    Id = row.id ?? 0,
                    Name = row.name ?? "—",
                    Ip = $"{row.ip}",
                    Port = row.port ?? 0,
                    Title = row.name ?? $"{row.address}",
                    Address = $"{row.address}",
                    Path = $"{row.remote_path}",
                    RecvAt = row.recv_mtime is null ? null : Micro(row.recv_mtime.Value),
                    Recv = row.recv_text ?? "—",
                    SentAt = row.sent_at,
                    Sent = row.sent_text ?? "—",
                    Ping = row.ping_ms,
                    PingAt = row.ping_at,
                    FailText = row.fail_text,
                    FailAt = row.fail_at,
                    FailCancelled = row.fail_cancelled == true,
                    InLast = row.in_last == true,
                })],

            // event 列：一次送出或測試一列；沒有錯誤寫 —
            Log = [.. rows
                .Where(row => row.type == "event")
                .Select(row => new LogRow
                {
                    At = $"{row.at_text}",
                    Machine = $"{row.name}",
                    EventType = $"{row.event_type}",
                    Address = $"{row.address}",
                    FileCount = row.file_count ?? 0,
                    KbText = $"{row.kb_text}",
                    RemotePath = $"{row.remote_path}",
                    Took = $"{row.took_text}",
                    Note = row.note ?? "—",
                    Noted = row.note is not null,
                    Error = row.note is not null && row.cancelled != true,
                })],
        };

    /// <summary>把現在的狀態算成畫面上的字：看得到的機台與先後、每一台的差與上次回應、工具列、來源、送出鈕</summary>
    /// <param name="machines">每一台機台，加上畫面上的勾與正在跑的那一件</param>
    /// <param name="files">來源的每一個檔</param>
    /// <param name="folder">來源資料夾；挑檔案時是空的</param>
    /// <param name="query">搜尋框的字</param>
    /// <param name="sortKey">照哪一欄排：name、addr、path、diff、ping</param>
    /// <param name="sortDesc">反過來排</param>
    /// <param name="busy">正在做的事：空的、send、test</param>
    /// <param name="now">這一刻（UTC）</param>
    public static ScreenView Screen(IReadOnlyList<MachineState> machines, IReadOnlyList<FileItem> files, string folder, string query, string sortKey, bool sortDesc, string busy, DateTime now)
    {
        var locked = busy.Length > 0;
        DateTime? local = files.Count > 0 ? files.Max(one => one.Mtime) : null;
        var total = files.Sum(one => one.Bytes);
        var picked = machines.Count(one => one.Checked);

        // 搜尋：名稱、IP:Port、遠端路徑任一段包含就符合
        var q = query.Trim();
        bool Fits(MachineState one) => q.Length == 0 || new[] { one.Row.Name, one.Row.Address, one.Row.Path }.Any(v => v.Contains(q, StringComparison.OrdinalIgnoreCase));

        // 排序：照那一欄的值；一樣的照名稱
        IComparable Value(MachineState one) => sortKey switch
        {
            "name" => one.Row.Name,
            "addr" => string.Join(".", one.Row.Ip.Split('.').Select(part => part.PadLeft(3, '0'))) + ":" + one.Row.Port.ToString("00000", CultureInfo.InvariantCulture),
            "path" => one.Row.Path,
            "diff" => one.Row.RecvAt is null ? long.MaxValue : -one.Row.RecvAt.Value.Ticks,
            _ => one.Row.FailText is not null && !one.Row.FailCancelled ? long.MaxValue : (long?)one.Row.Ping ?? long.MinValue,
        };
        int Order(MachineState a, MachineState b)
        {
            var x = Value(a);
            var y = Value(b);
            var c = x is string s ? Words.Compare(s, (string)y) : x.CompareTo(y);
            return c != 0 ? (sortDesc ? -c : c) : Words.Compare(a.Row.Name, b.Row.Name);
        }

        var shown = machines.Where(Fits).ToList();
        shown.Sort(Order);

        // 每一台：差、上次回應、送的時候的進度
        var each = machines.ToDictionary(one => one.Row.Id, one =>
        {
            var row = one.Row;
            var job = one.Job;
            var sending = job is { Mode: "send" };
            var testing = job is { Mode: "test" };
            var failed = !testing && row.FailText is not null;
            var sent = sending ? Interlocked.Read(ref job!.Sent) : 0;

            // 上次回應：測試中寫等了多久；上次沒成功寫原因；不然寫回應的毫秒與多久以前
            var reply = testing ? $"連線中 {(now - job!.Started).TotalSeconds:0.0} 秒"
                : failed ? row.FailText!
                : row.Ping is null ? "—" : $"{row.Ping} ms · {Ago(row.PingAt ?? now, now)}";

            return new MachineShown
            {
                Free = !locked,
                Editable = job is null,
                Sending = sending,
                Idle = !sending,
                PathTitle = row.Path.Length > 0 ? row.Path : "還沒填遠端路徑",
                DiffTitle = $"機台那份 {row.Recv} · 上次送達 {row.Sent}",
                Diff = local is null || row.RecvAt is null ? "—"
                    : local.Value == row.RecvAt.Value ? "相同"
                    : (local.Value > row.RecvAt.Value ? "本機新 " : "機台新 ") + Span((local.Value - row.RecvAt.Value).Duration()),

                Reply = reply,
                ReplyTitle = row.FailText is not null && job is null ? $"{Ago(row.FailAt ?? now, now)}：{row.FailText}" : reply,
                Failed = failed,
                Cancelled = row.FailCancelled,

                // 送的時候：連線中寫等了多久；傳送中寫送了多少、多快
                Done = !sending || job!.Phase == "connect" ? 0 : job.Total == 0 ? 1 : (double)sent / job.Total,
                Progress = !sending ? string.Empty
                    : job!.Phase == "connect" ? $"連線到 {row.Address} · {(now - job.Started).TotalSeconds:0.0} 秒"
                    : $"{Kb(sent)} / {Kb(job.Total)} KB · {Math.Round(sent / 1024.0 / Math.Max(0.001, (now - job.SendAt).TotalSeconds))} KB/s · {(now - job.Started).TotalSeconds:0.0} 秒",
            };
        });

        // 送的時候的整體進度
        var running = machines.Where(one => one.Job is { Mode: "send" }).Select(one => one.Job!).ToList();
        var since = running.Count > 0 ? running.Min(one => one.Started) : now;
        var allOn = shown.Count > 0 && shown.All(one => one.Checked);

        return new ScreenView
        {
            Shown = [.. shown.Select(one => one.Row.Id)],
            Machines = each,
            Has = machines.Count > 0,
            AllChecked = allOn ? true : shown.Any(one => one.Checked) ? null : false,
            ShownCount = shown.Count,
            Querying = q.Length > 0,
            LastCount = machines.Count(one => one.Row.InLast),
            LastTitle = string.Join("、", machines.Where(one => one.Row.InLast).Select(one => one.Row.Title)),
            Picked = picked,
            Locked = locked,
            Where = folder.Length > 0 ? folder : "—",
            FileSummary = files.Count > 0 ? $"{files.Count} 個 · {Kb(total)} KB" : "—",
            LocalTime = Time(local, now),
            Overall = $"{running.Count} 台 · {Kb(running.Sum(one => Interlocked.Read(ref one.Sent)))} / {Kb(running.Sum(one => one.Total))} KB · {(now - since).TotalSeconds:0.0} 秒",
            SendNote = busy == "test" ? "正在測試連線"
                : files.Count == 0 ? "還沒選來源"
                : picked == 0 ? "還沒勾要送的機台"
                : $"{files.Count} 個檔 · {Kb(total)} KB → 各台自己的遠端路徑",
            CanSend = !locked && files.Count > 0 && picked > 0,
        };
    }

    #endregion

    #region 來源

    /// <summary>來源換成一個資料夾：每一個檔在資料夾底下的相對路徑，新的在前</summary>
    /// <param name="path">選的資料夾</param>
    /// <param name="found">資料夾（含子資料夾）裡讀到的每一個檔</param>
    /// <param name="now">這一刻（UTC）</param>
    public static SourceView Folder(string path, IReadOnlyList<FileItem> found, DateTime now)
    {
        // 資料夾一律以 \ 結尾
        var folder = path.TrimEnd('\\') + "\\";
        var files = found
            .Select(one => new FileItem { Full = one.Full, Relative = System.IO.Path.GetRelativePath(folder, one.Full), Bytes = one.Bytes, Mtime = Micro(one.Mtime) })
            .OrderByDescending(one => one.Mtime).ThenBy(one => one.Relative, Words).ToList();

        return new SourceView
        {
            Folder = folder,
            Files = files,
            Shown = [.. files.Select(one => new FileShown { Name = Middle(one.Relative, 44), Full = one.Relative, Size = Size(one.Bytes), Time = Time(one.Mtime, now) })],
            Text = $"來源換成 {folder}，{files.Count} 個檔",
        };
    }

    /// <summary>來源換成挑的那幾個檔：只寫檔名，新的在前</summary>
    /// <param name="found">挑的每一個檔</param>
    /// <param name="now">這一刻（UTC）</param>
    public static SourceView Picked(IReadOnlyList<FileItem> found, DateTime now)
    {
        var files = found
            .Select(one => new FileItem { Full = one.Full, Relative = System.IO.Path.GetFileName(one.Full), Bytes = one.Bytes, Mtime = Micro(one.Mtime) })
            .OrderByDescending(one => one.Mtime).ThenBy(one => one.Relative, Words).ToList();

        return new SourceView
        {
            Folder = string.Empty,
            Files = files,
            Shown = [.. files.Select(one => new FileShown { Name = Middle(one.Relative, 44), Full = one.Relative, Size = Size(one.Bytes), Time = Time(one.Mtime, now) })],
            Text = $"來源換成挑的 {files.Count} 個檔",
        };
    }

    #endregion

    #region 送出與測試

    /// <summary>送出開始：勾的每一台一列 send，記下開始的時刻、送到哪裡、送什麼；沒填遠端路徑的那台當場結束並寫下原因</summary>
    /// <param name="rows">vw_vncscp 查到的每一列</param>
    /// <param name="picked">勾的那幾台的編號</param>
    /// <param name="files">要送的每一個檔</param>
    /// <param name="batch">這一批的編號</param>
    /// <param name="now">這一刻（UTC）</param>
    public static Started SendStarted(IReadOnlyList<vw_vncscp> rows, IReadOnlyList<int> picked, IReadOnlyList<FileItem> files, Guid batch, DateTime now)
    {
        // 沒有檔就不送
        var chosen = rows.Where(row => row.type == "machine" && files.Count > 0 && picked.Contains(row.id ?? 0)).ToList();
        var total = files.Sum(one => one.Bytes);
        DateTime? local = files.Count > 0 ? files.Max(one => one.Mtime) : null;

        // 每台一列；遠端路徑空的那台結束的時刻與原因一起寫
        var runs = chosen.Select(row => new
        {
            Machine = row,
            Path = $"{row.remote_path}".Trim(),
            Row = new vncscp
            {
                type = "send",
                machine_id = row.id,
                batch = batch,
                remote_path = $"{row.remote_path}".Trim(),
                file_count = files.Count,
                total_bytes = total,
                sent_bytes = 0,
                local_mtime = local,
                started_at = now,
                finished_at = $"{row.remote_path}".Trim().Length == 0 ? now : null,
                note = $"{row.remote_path}".Trim().Length == 0 ? NoPath : null,
                updated_at = now,
            },
        }).ToList();

        return new Started
        {
            Rows = [.. runs.Select(one => one.Row)],
            Jobs = [.. runs.Where(one => one.Path.Length > 0).Select(one => new Job
            {
                Mode = "send",
                MachineId = one.Machine.id ?? 0,
                Ip = $"{one.Machine.ip}",
                Port = one.Machine.port ?? 0,
                Title = one.Machine.name ?? $"{one.Machine.address}",
                Row = one.Row,
                Started = now,
                Total = total,
                Count = files.Count,
                Path = one.Path,
            })],
            Skipped = [.. runs.Where(one => one.Path.Length == 0).Select(one => $"{one.Machine.name ?? $"{one.Machine.address}"}沒送到：{NoPath}")],
            Files = [.. files.Select(one => (one.Full, one.Relative))],
            Count = files.Count,
            Total = total,
        };
    }

    /// <summary>測試開始：勾的每一台一列 test，記下開始的時刻</summary>
    /// <param name="rows">vw_vncscp 查到的每一列</param>
    /// <param name="picked">勾的那幾台的編號</param>
    /// <param name="batch">這一批的編號</param>
    /// <param name="now">這一刻（UTC）</param>
    public static Started TestStarted(IReadOnlyList<vw_vncscp> rows, IReadOnlyList<int> picked, Guid batch, DateTime now)
    {
        var runs = rows
            .Where(row => row.type == "machine" && picked.Contains(row.id ?? 0))
            .Select(row => new
            {
                Machine = row,
                Row = new vncscp { type = "test", machine_id = row.id, batch = batch, started_at = now, updated_at = now },
            })
            .ToList();

        return new Started
        {
            Rows = [.. runs.Select(one => one.Row)],
            Jobs = [.. runs.Select(one => new Job
            {
                Mode = "test",
                MachineId = one.Machine.id ?? 0,
                Ip = $"{one.Machine.ip}",
                Port = one.Machine.port ?? 0,
                Title = one.Machine.name ?? $"{one.Machine.address}",
                Row = one.Row,
                Started = now,
            })],
        };
    }

    /// <summary>按了取消：送到一半的寫送了多少，連線中的與測試中的寫等了多久</summary>
    /// <param name="job">那一台正在跑的那一件</param>
    /// <param name="at">按取消後停下的時刻（UTC）</param>
    public static string Stopped(Job job, DateTime at) =>
        job.Mode == "test" ? $"等了 {(at - job.Started).TotalSeconds:0.0} 秒沒有回應，取消"
        : job.Phase == "send" ? $"傳到 {Kb(Interlocked.Read(ref job.Sent))} / {Kb(job.Total)} KB 時取消"
        : $"連線 {(at - job.Started).TotalSeconds:0.0} 秒時取消";

    /// <summary>一台結束：開始時寫的那一列整列帶過來，換上登入花的毫秒、送到的位元組、結束與取消的時刻、原因</summary>
    /// <param name="job">那一台跑完的那一件</param>
    /// <param name="now">結束的時刻（UTC）</param>
    public static vncscp Finished(Job job, DateTime now) =>
        new()
        {
            id = job.Row.id,
            type = job.Row.type,
            machine_id = job.Row.machine_id,
            batch = job.Row.batch,
            remote_path = job.Row.remote_path,
            file_count = job.Row.file_count,
            total_bytes = job.Row.total_bytes,
            sent_bytes = job.Mode == "send" ? Interlocked.Read(ref job.Sent) : job.Row.sent_bytes,
            local_mtime = job.Row.local_mtime,
            started_at = job.Row.started_at,
            connected_ms = job.Connected,
            finished_at = now,
            cancelled_at = job.Cancelled,
            note = job.Note,
            updated_at = now,
        };

    /// <summary>一批送出結束：成功的合成一則，失敗的每台一則；按了取消就只說取消了</summary>
    /// <param name="start">這一批開始時算的</param>
    /// <param name="stopped">按取消時還在跑的台數；沒按是 0</param>
    public static List<Notice> Sent(Started start, int stopped)
    {
        var ok = start.Jobs.Where(one => one.Note is null).OrderBy(one => one.Ended).Select(one => one.Title).ToList();
        var failed = start.Skipped
            .Concat(start.Jobs.Where(one => one.Note is not null && one.Cancelled is null).OrderBy(one => one.Ended).Select(one => $"{one.Title}沒送到：{one.Note}"))
            .ToList();

        return stopped > 0
            ? [new Notice { Text = $"已取消，{stopped} 台沒送完：機台上可能只換了部分檔案" }]
            : [.. new[] { ok }.Where(one => one.Count > 0).Select(one => new Notice { Text = $"已送到 {one.Count} 台：{string.Join("、", one)}（{start.Count} 個檔，{Kb(start.Total)} KB）" }),
               .. failed.Select(text => new Notice { Text = text, Error = true })];
    }

    /// <summary>一批測試結束：連上的合成一則，連不上的每台一則；按了取消就說幾台還沒回應</summary>
    /// <param name="start">這一批開始時算的</param>
    /// <param name="stopped">按取消時還在跑的台數；沒按是 0</param>
    public static List<Notice> Tested(Started start, int stopped)
    {
        var ok = start.Jobs.Where(one => one.Note is null).OrderBy(one => one.Ended).Select(one => $"{one.Title} {one.Connected} ms").ToList();
        var reached = ok.Count > 0 ? $"連上 {ok.Count} 台：{string.Join("、", ok)}" : string.Empty;
        var failed = start.Jobs.Where(one => one.Note is not null && one.Cancelled is null).OrderBy(one => one.Ended).Select(one => $"{one.Title}：{one.Note}").ToList();

        return stopped > 0
            ? [new Notice { Text = $"已取消測試：{stopped} 台還沒回應{(reached.Length > 0 ? "；" + reached : string.Empty)}" }]
            : [.. new[] { reached }.Where(one => one.Length > 0).Select(one => new Notice { Text = one }),
               .. failed.Select(text => new Notice { Text = text, Error = true })];
    }

    #endregion

    #region 基本 CRUD 操作

    /// <summary>加入一台：IP 與 Port 都要填、Port 是 1 到 65535、同一個 IP:Port 不能加兩次；錯的只回第一格，畫面把游標放回那一格</summary>
    /// <param name="rows">vw_vncscp 查到的每一列</param>
    /// <param name="data">新增機台那一列打的字</param>
    /// <param name="now">這一刻（UTC）</param>
    public static AddSaved Added(IReadOnlyList<vw_vncscp> rows, MachineInput data, DateTime now)
    {
        var name = $"{data.name}".Trim();
        var ip = $"{data.ip}".Trim();
        var portText = $"{data.port}".Trim();
        var valid = int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port >= 1 && port <= 65535;
        var same = rows.Where(row => row.type == "machine" && row.ip == ip && row.port == port).ToList();

        // 寫不成的原因，照順序；只回第一個
        var faults = new[]
            {
                (Field: "ip", Fault: ip.Length == 0, Text: "先填 IP，例如 192.168.10.23"),
                (Field: "port", Fault: portText.Length == 0, Text: "先填 Port，機台上 UltraVNC 用的那個，例如 5900"),
                (Field: "port", Fault: portText.Length > 0 && !valid, Text: $"Port「{portText}」不能用：要是 1 到 65535 的數字"),
                (Field: "ip", Fault: valid && same.Count > 0, Text: $"{ip}:{port} 已經在清單裡（{same.Select(row => row.name ?? "—").FirstOrDefault()}）"),
            }
            .Where(one => one.Fault)
            .ToList();

        return new AddSaved
        {
            Field = faults.Select(one => one.Field).DefaultIfEmpty(string.Empty).First(),
            Fault = faults.Select(one => one.Text).DefaultIfEmpty(string.Empty).First(),

            // 名稱空著就不填；遠端路徑之後在那一列填
            Row = [.. new[] { data }.Where(_ => faults.Count == 0).Select(_ => new vncscp
            {
                type = "machine",
                name = name.Length > 0 ? name : null,
                ip = ip,
                port = port,
                updated_at = now,
            })],
            Text = $"已加入 {(name.Length > 0 ? name : ip)}（{ip}:{port}）",
        };
    }

    /// <summary>改一台的遠端路徑：那一台從 vw_vncscp 的 machine 列整列帶過來，換上新的路徑；沒變就不寫</summary>
    /// <param name="rows">vw_vncscp 查到的：每一項設定與那一台</param>
    /// <param name="id">那一台的編號</param>
    /// <param name="path">新的遠端路徑</param>
    /// <param name="now">這一刻（UTC）</param>
    public static List<vncscp> PathChanged(IReadOnlyList<vw_vncscp> rows, int id, string path, DateTime now) =>
        [.. rows
            .Where(row => row.type == "machine" && row.id == id && $"{row.remote_path}" != path.Trim())
            .Select(row => new vncscp
            {
                id = row.id ?? 0,
                type = "machine",
                name = row.name,
                ip = row.ip,
                port = row.port,
                remote_path = path.Trim(),
                updated_at = now,
            })];

    /// <summary>改完遠端路徑的那一則：機台那份與上次送達換成新位置的（沒送過就說還沒送過）</summary>
    /// <param name="rows">寫完之後 vw_vncscp 查到的：每一項設定與那一台</param>
    /// <param name="id">那一台的編號</param>
    /// <param name="written">寫回去的那幾列；沒變就是零筆，不說</param>
    public static string PathMoved(IReadOnlyList<vw_vncscp> rows, int id, IReadOnlyList<vncscp> written) =>
        rows
            .Where(row => row.type == "machine" && row.id == id && written.Count > 0)
            .Select(row => $"{row.remote_path}".Length == 0 ? $"{row.name ?? row.address} 的遠端路徑清空了"
                : row.recv_mtime is null ? $"{row.name ?? row.address} 的遠端路徑改成 {row.remote_path}，這個位置還沒送過"
                : $"{row.name ?? row.address} 的遠端路徑改成 {row.remote_path}")
            .DefaultIfEmpty(string.Empty)
            .First();

    /// <summary>記下來源資料夾：source 那一項設定從 vw_vncscp 帶過來，換上新的路徑</summary>
    /// <param name="rows">vw_vncscp 查到的：每一項設定</param>
    /// <param name="folder">來源資料夾</param>
    /// <param name="now">這一刻（UTC）</param>
    public static List<vncscp> SourceChanged(IReadOnlyList<vw_vncscp> rows, string folder, DateTime now) =>
        [.. rows
            .Where(row => row.type == "setting")
            .Select(row => new vncscp { id = row.id ?? 0, type = "setting", setting_key = "source", path = folder, updated_at = now })];

    #endregion

    #region 字

    /// <summary>KB，一位小數、千分位</summary>
    /// <param name="bytes">位元組</param>
    private static string Kb(long bytes) =>
        (bytes / 1024.0).ToString("#,##0.0", CultureInfo.InvariantCulture);

    /// <summary>檔案大小：B、KB、MB</summary>
    /// <param name="bytes">位元組</param>
    private static string Size(long bytes) =>
        bytes < 1024 ? $"{bytes} B" : bytes < 1024 * 1024 ? $"{Kb(bytes)} KB" : $"{bytes / 1024.0 / 1024.0:0.00} MB";

    /// <summary>時間：今年的寫 月-日 時:分，不是今年的寫 年-月-日；沒有寫 —</summary>
    /// <param name="at">時間（UTC）</param>
    /// <param name="now">現在（UTC）</param>
    private static string Time(DateTime? at, DateTime now) =>
        at is null ? "—"
        : at.Value.ToLocalTime().Year != now.ToLocalTime().Year ? at.Value.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        : at.Value.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>一段時間：秒、分、時 分、天 時</summary>
    /// <param name="span">多久</param>
    private static string Span(TimeSpan span)
    {
        var s = (long)Math.Round(span.TotalSeconds);
        var m = s / 60;
        var h = m / 60;
        return s < 60 ? $"{s} 秒" : m < 60 ? $"{m} 分" : h < 24 ? $"{h} 時 {m % 60} 分" : $"{h / 24} 天 {h % 24} 時";
    }

    /// <summary>多久以前：秒前、分前、時前、天前</summary>
    /// <param name="at">那時（UTC）</param>
    /// <param name="now">現在（UTC）</param>
    private static string Ago(DateTime at, DateTime now)
    {
        var s = Math.Max(1, (long)Math.Round((now - at.ToUniversalTime()).TotalSeconds));
        var m = s / 60;
        var h = m / 60;
        return s < 60 ? $"{s} 秒前" : m < 60 ? $"{m} 分前" : h < 24 ? $"{h} 時前" : $"{h / 24} 天前";
    }

    /// <summary>名稱太長從中間截，留住副檔名</summary>
    /// <param name="name">名稱</param>
    /// <param name="max">最多幾個字</param>
    private static string Middle(string name, int max) =>
        name.Length <= max ? name : name[..(max - (int)Math.Floor(max * 0.4) - 1)] + "…" + name[^(int)Math.Floor(max * 0.4)..];

    /// <summary>時間只留到微秒：資料庫存到微秒，兩邊才比得出「相同」</summary>
    /// <param name="at">時間（UTC）</param>
    private static DateTime Micro(DateTime at) =>
        new(at.Ticks - at.Ticks % 10, DateTimeKind.Utc);

    #endregion

    #region 對照

    /// <summary>沒填遠端路徑的那台記下的原因</summary>
    private const string NoPath = "沒有遠端路徑：先在這一列填上要放的資料夾";

    /// <summary>名稱照中文的先後比</summary>
    private static readonly StringComparer Words = StringComparer.Create(CultureInfo.GetCultureInfo("zh-TW"), false);

    #endregion

    #region 模型

    /// <summary>主畫面讀出來的那一份</summary>
    public class BoardView
    {
        /// <summary>上次選的來源資料夾；還沒選是空的</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>每一台機台</summary>
        public List<MachineRow> Machines { get; set; } = [];

        /// <summary>最近送出與測試，新的在前</summary>
        public List<LogRow> Log { get; set; } = [];
    }

    /// <summary>一台機台：vw_vncscp 的 machine 列</summary>
    public class MachineRow
    {
        /// <summary>machine 那一列的編號</summary>
        public int Id { get; set; }

        /// <summary>名稱；空的寫 —；長度照資料表欄位的定義</summary>
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        /// <summary>IP；長度照資料表欄位的定義</summary>
        [StringLength(45)]
        public string Ip { get; set; } = string.Empty;

        /// <summary>UltraVNC 的 Port</summary>
        public int Port { get; set; }

        /// <summary>通知裡的稱呼：名稱，空的寫 IP:Port</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>IP:Port</summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>現在的遠端路徑</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>機台那份的修改時間（UTC，到微秒）；送到現在這個遠端路徑的最後一次成功</summary>
        public DateTime? RecvAt { get; set; }

        /// <summary>機台那份的字</summary>
        public string Recv { get; set; } = string.Empty;

        /// <summary>那一次送完的時刻</summary>
        public DateTime? SentAt { get; set; }

        /// <summary>上次送達的字</summary>
        public string Sent { get; set; } = string.Empty;

        /// <summary>最後一次登入成功的毫秒</summary>
        public int? Ping { get; set; }

        /// <summary>那一次的開始時刻</summary>
        public DateTime? PingAt { get; set; }

        /// <summary>最後一次沒成功的原因；之後成功過就是 null</summary>
        public string? FailText { get; set; }

        /// <summary>那一次結束的時刻</summary>
        public DateTime? FailAt { get; set; }

        /// <summary>那一次是使用者按了取消</summary>
        public bool FailCancelled { get; set; }

        /// <summary>最後一批送出有沒有這一台</summary>
        public bool InLast { get; set; }
    }

    /// <summary>最近送出與測試的一列：vw_vncscp 的 event 列</summary>
    public class LogRow
    {
        /// <summary>開始的時刻</summary>
        public string At { get; set; } = string.Empty;

        /// <summary>機台</summary>
        public string Machine { get; set; } = string.Empty;

        /// <summary>send＝送出；test＝測試連線（畫面上的字寫在 XAML）</summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>IP:Port；測試那一列的內容</summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>送幾個檔</summary>
        public int FileCount { get; set; }

        /// <summary>要送的總大小 KB</summary>
        public string KbText { get; set; } = string.Empty;

        /// <summary>這一次送到的路徑</summary>
        public string RemotePath { get; set; } = string.Empty;

        /// <summary>耗時</summary>
        public string Took { get; set; } = string.Empty;

        /// <summary>沒成功的原因；成功寫 —</summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>有原因</summary>
        public bool Noted { get; set; }

        /// <summary>有原因而且不是按了取消：紅字</summary>
        public bool Error { get; set; }
    }

    /// <summary>算畫面時的一台：機台那一列、畫面上的勾、正在跑的那一件</summary>
    public class MachineState
    {
        /// <summary>vw_vncscp 的 machine 列</summary>
        public MachineRow Row { get; set; } = new();

        /// <summary>勾了沒</summary>
        public bool Checked { get; set; }

        /// <summary>正在跑的那一件；沒有是 null</summary>
        public Job? Job { get; set; }
    }

    /// <summary>畫面上一台要寫的字與能不能動</summary>
    public class MachineShown
    {
        /// <summary>能不能勾：跑的時候不能</summary>
        public bool Free { get; set; }

        /// <summary>遠端路徑能不能改：這一台在跑的時候不能</summary>
        public bool Editable { get; set; }

        /// <summary>正在送</summary>
        public bool Sending { get; set; }

        /// <summary>沒在送：顯示差與上次回應</summary>
        public bool Idle { get; set; }

        /// <summary>遠端路徑的提示：路徑本身，空的寫還沒填</summary>
        public string PathTitle { get; set; } = string.Empty;

        /// <summary>差：本機新／機台新多久、相同、—</summary>
        public string Diff { get; set; } = string.Empty;

        /// <summary>差的提示：機台那份與上次送達</summary>
        public string DiffTitle { get; set; } = string.Empty;

        /// <summary>上次回應的字</summary>
        public string Reply { get; set; } = string.Empty;

        /// <summary>上次回應的提示</summary>
        public string ReplyTitle { get; set; } = string.Empty;

        /// <summary>上次回應寫的是沒成功的原因（靠左；紅或灰照 Cancelled）</summary>
        public bool Failed { get; set; }

        /// <summary>那一次是使用者按了取消</summary>
        public bool Cancelled { get; set; }

        /// <summary>送了幾成（0～1）</summary>
        public double Done { get; set; }

        /// <summary>進度的字</summary>
        public string Progress { get; set; } = string.Empty;
    }

    /// <summary>畫面上的字與看得到的機台</summary>
    public class ScreenView
    {
        /// <summary>看得到的機台編號，照排序</summary>
        public List<int> Shown { get; set; } = [];

        /// <summary>每一台要寫的字，照編號取</summary>
        public Dictionary<int, MachineShown> Machines { get; set; } = [];

        /// <summary>有沒有機台</summary>
        public bool Has { get; set; }

        /// <summary>看得到的全勾是 true、勾了一些是 null、都沒勾是 false</summary>
        public bool? AllChecked { get; set; }

        /// <summary>看得到幾台</summary>
        public int ShownCount { get; set; }

        /// <summary>搜尋框有字</summary>
        public bool Querying { get; set; }

        /// <summary>最後一批送出有幾台</summary>
        public int LastCount { get; set; }

        /// <summary>最後一批送出的那幾台的稱呼，以頓號隔開</summary>
        public string LastTitle { get; set; } = string.Empty;

        /// <summary>勾了幾台</summary>
        public int Picked { get; set; }

        /// <summary>正在送或測，不能改</summary>
        public bool Locked { get; set; }

        /// <summary>來源的位置</summary>
        public string Where { get; set; } = string.Empty;

        /// <summary>來源有幾個檔、多大</summary>
        public string FileSummary { get; set; } = string.Empty;

        /// <summary>來源最新的修改時間</summary>
        public string LocalTime { get; set; } = string.Empty;

        /// <summary>送的時候的整體進度</summary>
        public string Overall { get; set; } = string.Empty;

        /// <summary>送出鈕上面那一行</summary>
        public string SendNote { get; set; } = string.Empty;

        /// <summary>送出鈕按得下去</summary>
        public bool CanSend { get; set; }
    }

    /// <summary>來源的一個檔</summary>
    public class FileItem
    {
        /// <summary>本機完整路徑</summary>
        public string Full { get; set; } = string.Empty;

        /// <summary>在來源資料夾底下的相對路徑；挑檔案時是檔名</summary>
        public string Relative { get; set; } = string.Empty;

        /// <summary>大小</summary>
        public long Bytes { get; set; }

        /// <summary>修改時間（UTC）</summary>
        public DateTime Mtime { get; set; }
    }

    /// <summary>來源清單的一列</summary>
    public class FileShown
    {
        /// <summary>名稱（太長從中間截）</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>完整的相對路徑</summary>
        public string Full { get; set; } = string.Empty;

        /// <summary>大小的字</summary>
        public string Size { get; set; } = string.Empty;

        /// <summary>修改時間的字</summary>
        public string Time { get; set; } = string.Empty;
    }

    /// <summary>來源：資料夾、每一個檔、清單上的每一列、換來源時的那一則</summary>
    public class SourceView
    {
        /// <summary>來源資料夾；挑檔案時是空的</summary>
        public string Folder { get; set; } = string.Empty;

        /// <summary>每一個檔，新的在前</summary>
        public List<FileItem> Files { get; set; } = [];

        /// <summary>來源清單的每一列</summary>
        public List<FileShown> Shown { get; set; } = [];

        /// <summary>換來源時的那一則</summary>
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>新增機台那一列打的字</summary>
    public class MachineInput
    {
        /// <summary>名稱，可以空著；長度照資料表欄位的定義</summary>
        [StringLength(50)]
        public string? name { get; set; }

        /// <summary>IP；長度照資料表欄位的定義</summary>
        [StringLength(45)]
        public string? ip { get; set; }

        /// <summary>Port 那一格的字</summary>
        public string? port { get; set; }

        /// <summary>VNC 密碼；不進資料庫，鎖在平台保險箱</summary>
        public string? password { get; set; }
    }

    /// <summary>加入一台之後要寫回去的那一列，與錯在哪一格、怎麼改</summary>
    public class AddSaved
    {
        /// <summary>錯的那一格：ip、port；沒錯是空的</summary>
        public string Field { get; set; } = string.Empty;

        /// <summary>那一格怎麼改；沒錯是空的</summary>
        public string Fault { get; set; } = string.Empty;

        /// <summary>要寫進 vncscp 的那一列；錯了是零筆</summary>
        public List<vncscp> Row { get; set; } = [];

        /// <summary>加入之後的那一則</summary>
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>一批送出或測試開始時算的</summary>
    public class Started
    {
        /// <summary>要寫進 vncscp 的每一列</summary>
        public List<vncscp> Rows { get; set; } = [];

        /// <summary>真的要連的每一台</summary>
        public List<Job> Jobs { get; set; } = [];

        /// <summary>沒填遠端路徑、當場結束的每一台的那一則</summary>
        public List<string> Skipped { get; set; } = [];

        /// <summary>要送的每一個檔：本機完整路徑與相對路徑</summary>
        public List<(string Local, string Relative)> Files { get; set; } = [];

        /// <summary>送幾個檔</summary>
        public int Count { get; set; }

        /// <summary>每台要送的總位元組</summary>
        public long Total { get; set; }
    }

    /// <summary>一台正在跑的那一件</summary>
    public class Job
    {
        /// <summary>send 或 test</summary>
        public string Mode { get; set; } = string.Empty;

        /// <summary>connect（連線中）或 send（傳送中）</summary>
        public string Phase { get; set; } = "connect";

        /// <summary>哪一台（machine 那一列的編號）</summary>
        public int MachineId { get; set; }

        /// <summary>那一台的 IP</summary>
        public string Ip { get; set; } = string.Empty;

        /// <summary>那一台的 Port</summary>
        public int Port { get; set; }

        /// <summary>那一台的稱呼</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>開始時寫的那一列 send／test；寫完帶著資料庫給的編號</summary>
        public vncscp Row { get; set; } = new();

        /// <summary>開始連線的時刻（UTC）</summary>
        public DateTime Started { get; set; }

        /// <summary>開始傳送的時刻（UTC）</summary>
        public DateTime SendAt { get; set; }

        /// <summary>結束的時刻（UTC）</summary>
        public DateTime Ended { get; set; }

        /// <summary>登入花了幾毫秒；沒登入成功是 null</summary>
        public int? Connected { get; set; }

        /// <summary>要送的總位元組</summary>
        public long Total { get; set; }

        /// <summary>要送幾個檔</summary>
        public int Count { get; set; }

        /// <summary>送到哪裡</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>按了取消的時刻；沒按是 null</summary>
        public DateTime? Cancelled { get; set; }

        /// <summary>沒成功的原因；成功是 null</summary>
        public string? Note { get; set; }

        /// <summary>已經送了多少位元組（傳送的那條執行緒在寫）</summary>
        public long Sent;
    }

    /// <summary>一批送出結束後交回畫面的：重讀後的來源與通知</summary>
    public class SentView
    {
        /// <summary>重讀後的來源</summary>
        public SourceView Source { get; set; } = new();

        /// <summary>一批結束時的通知；重讀後沒有檔、沒送是零筆</summary>
        public List<Notice> Notices { get; set; } = [];
    }

    /// <summary>一則通知</summary>
    public class Notice
    {
        /// <summary>內容</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>錯誤（留到按關閉）還是資訊（4 秒收起）</summary>
        public bool Error { get; set; }
    }

    #endregion
}
