using System.Globalization;
using System.Windows.Media;
using Models.HelixStudio;
using ProcessMonitor.Store;

namespace ProcessMonitor.Logic;

/// <summary>程式監控的純運算</summary>
/// <remarks>
/// 不碰資料庫、不問 Windows、不碰畫面，給什麼算什麼；沒有狀態，所以是靜態的。量到的值、上一次量到的值、vw_monitor 的列都由 Service 交進來。
/// 畫面上的字（事件名、優先順序名、句型）寫在 Board.xaml 的 Words，由 Service 交進來；這裡只把值填進去。
/// </remarks>
public static class MonitorLogic
{
    #region 量一次

    /// <summary>量一次：同一個路徑的程序併成一支程式（沒有路徑的照名字併），算這一秒的 CPU、讀寫、網路；沒有回應的從第一次看到起算，恢復或結束的交出卡了多久</summary>
    /// <param name="processes">這一刻每一個程序</param>
    /// <param name="before">上一次量到的每一個程序：累計 CPU 時間與讀寫位元組</param>
    /// <param name="elapsed">離上一次幾秒</param>
    /// <param name="cores">CPU 核心數</param>
    /// <param name="traffic">這一秒每一個程序收到、送出的位元組；量不到網路是 null</param>
    /// <param name="files">每一個路徑的名稱與版本</param>
    /// <param name="hungSince">每一支程式從什麼時候開始沒有回應</param>
    /// <param name="now">這一刻</param>
    internal static SampleView Sampled(IReadOnlyList<WindowsStore.ProcessItem> processes, IReadOnlyDictionary<int, (long Cpu, ulong Io)> before, double elapsed, int cores,
        IReadOnlyDictionary<int, (long Down, long Up)>? traffic, IReadOnlyDictionary<string, (string Name, string Version)> files,
        IReadOnlyDictionary<string, DateTime> hungSince, DateTime now)
    {
        // 照路徑併成一支程式；每一個程序這一秒的 CPU % 與讀寫位元組 / 秒
        var programs = processes
            .GroupBy(one => one.Path.Length > 0 ? one.Path.ToLowerInvariant() : "name:" + one.Name.ToLowerInvariant())
            .Select(group =>
            {
                var first = group.OrderBy(one => one.Start ?? DateTime.MaxValue).First();
                var file = first.Path.Length > 0 && files.TryGetValue(first.Path, out var known) ? known : (Name: first.Name, Version: string.Empty);
                var rates = group.Select(one => before.TryGetValue(one.Pid, out var b)
                    ? (Cpu: Math.Max(0, one.Cpu - b.Cpu) / 1e7 / elapsed / cores * 100, Disk: one.Io >= b.Io ? (one.Io - b.Io) / elapsed : 0)
                    : (Cpu: 0, Disk: 0)).ToList();
                var pids = group.Select(one => one.Pid).ToList();

                return new ProgramItem
                {
                    Key = group.Key,
                    Path = first.Path,
                    Name = file.Name,
                    Version = file.Version,
                    Pids = pids,
                    Cpu = rates.Sum(one => one.Cpu),
                    Mem = group.Sum(one => one.Mem),
                    Down = traffic is null ? null : pids.Sum(one => traffic.GetValueOrDefault(one).Down) / elapsed,
                    Up = traffic is null ? null : pids.Sum(one => traffic.GetValueOrDefault(one).Up) / elapsed,
                    Disk = rates.Sum(one => one.Disk),
                    Gpu = group.Sum(one => one.Gpu),
                    Threads = group.Sum(one => one.Threads),
                    Handles = group.Sum(one => one.Handles),
                    Start = first.Start,
                    Priority = first.Priority,
                    Session = first.Session,
                    Critical = group.Any(one => one.Critical),
                    Hung = group.Any(one => one.Hung),
                    HungSince = group.Any(one => one.Hung) ? hungSince.GetValueOrDefault(group.Key, now) : null,
                };
            })
            .ToList();

        return new SampleView
        {
            At = now,
            Programs = programs,
            Before = processes.GroupBy(one => one.Pid).ToDictionary(group => group.Key, group => (group.First().Cpu, group.First().Io)),

            // 沒有回應：第一次看到的那一刻起算
            HungSince = programs.Where(one => one.Hung).ToDictionary(one => one.Key, one => one.HungSince!.Value),

            // 恢復回應或沒回應就結束了：卡了多久（Windows 5 秒沒處理訊息才判定，所以加 5 秒）
            Recovered = hungSince
                .Where(one => !programs.Any(two => two.Key == one.Key && two.Hung))
                .Select(one => new RecoveredItem
                {
                    Key = one.Key,
                    Seconds = (int)Math.Round((now - one.Value).TotalSeconds) + HungDelay,
                })
                .ToList(),
        };
    }

    #endregion

    #region 監控台

    /// <summary>攤成監控台：清單（照點的那一欄排、照搜尋字過濾）、選中那一支的明細與走勢與分頁、事件表、沒有回應的橫幅</summary>
    /// <param name="sample">這一次量到的</param>
    /// <param name="rows">vw_monitor 的每一列</param>
    /// <param name="sockets">這一刻每一條連線與聽著的埠</param>
    /// <param name="links">這一秒每一條連線（程序、遠端位址）收到、送出的位元組；量不到是 null</param>
    /// <param name="traces">選中那一支近 10 分鐘的走勢：cpu、mem、down、up</param>
    /// <param name="signers">每一個路徑的簽章者；還沒驗完的不在裡面</param>
    /// <param name="startups">每一個開機自動執行</param>
    /// <param name="modules">選中那一支載入的模組</param>
    /// <param name="listenSince">每一個聽著的埠第一次看到的時刻；開始監控時就在的是 null</param>
    /// <param name="paused">在這台暫停中的程式</param>
    /// <param name="eased">在這台緩和中的程式：沒有回應的那一支 → 被暫停的那一支</param>
    /// <param name="self">HelixStudio 自己的 Key（緩和不暫停它）</param>
    /// <param name="admin">有沒有系統管理員權限</param>
    /// <param name="netFault">網路量不到的原因；量得到是空字串</param>
    /// <param name="words">畫面上的字</param>
    /// <param name="picked">選中那一支的 Key</param>
    /// <param name="sort">照哪一欄排</param>
    /// <param name="descending">由大到小</param>
    /// <param name="keyword">搜尋框打的字</param>
    /// <param name="order">定住的清單順序（點過一列之後）：在裡面的照它排，新出現的接在最後；沒定住是 null，照點的那一欄排</param>
    /// <param name="tab">右邊看哪一個分頁：conn、port、event、module</param>
    /// <param name="width">走勢的寬（畫面上量到的）</param>
    internal static BoardView Board(SampleView sample, IReadOnlyList<vw_monitor> rows, IReadOnlyList<WindowsStore.SocketItem> sockets,
        IReadOnlyDictionary<(int Pid, string Remote), (long Down, long Up)>? links, IReadOnlyDictionary<string, double[]> traces,
        IReadOnlyDictionary<string, string> signers, IReadOnlyList<WindowsStore.StartupItem> startups, IReadOnlyList<(string Name, string Version)> modules,
        IReadOnlyDictionary<(string Key, string Protocol, string Local), DateTime?> listenSince, IReadOnlySet<string> paused, IReadOnlyDictionary<string, string> eased,
        string self, bool admin, string netFault, IReadOnlyDictionary<string, string> words, string picked, string sort, bool descending, string keyword, IReadOnlyList<string>? order, string tab, double width)
    {
        var netShown = admin && netFault.Length == 0;

        // 每一支程式的路徑（小寫）對到它的限制列與閃退次數
        var limits = rows.Where(row => row.type == "limit").GroupBy(row => $"{row.exe_path}".ToLowerInvariant()).ToDictionary(group => group.Key, group => group.First());
        var crashes = rows.Where(row => row.type == "crashes").GroupBy(row => $"{row.exe_path}".ToLowerInvariant()).ToDictionary(group => group.Key, group => group.First().crash_count ?? 0);

        // 排成欄的量測項目：存著的那一份，沒存過就是預設的那幾項
        var saved = rows.Where(row => row.type == "columns").Select(row => row.columns ?? []).FirstOrDefault();
        var shown = Metrics.Where(one => saved is null ? one.Shown : saved.Contains(one.Key)).ToList();

        // 一格的值（排序用）與字
        string Signer(ProgramItem p) => p.Path.Length == 0 ? Blank : signers.TryGetValue(p.Path, out var s) ? (s.Length == 0 ? words["signer.none"] : s) : string.Empty;

        object Value(ProgramItem p, string key) => key switch
        {
            "name" => p.Name,
            "cpu" => p.Cpu,
            "mem" => (double)p.Mem,
            "down" => p.Down ?? -1,
            "up" => p.Up ?? -1,
            "disk" => p.Disk,
            "run" => p.Start is null ? -1 : (sample.At - p.Start.Value).TotalSeconds,
            "crash" => (double)crashes.GetValueOrDefault(p.Path.ToLowerInvariant()),
            "signer" => Signer(p),
            "rule" => Rule(p, limits.GetValueOrDefault(p.Path.ToLowerInvariant()), paused, words),
            "gpu" => p.Gpu,
            "threads" => (double)p.Threads,
            "handles" => (double)p.Handles,
            _ => 0d,
        };

        string Text(ProgramItem p, MetricItem m) => m.Key switch
        {
            "down" or "up" when !netShown => Blank,
            "signer" or "rule" => (string)Value(p, m.Key),
            "run" => p.Start is null ? Blank : Span(sample.At - p.Start.Value, words),
            _ => Format(m.Unit, (double)Value(p, m.Key)),
        };

        // 清單：名稱、路徑或簽章者有打的字的每一支；照點的那一欄排，同一個值照名稱；定住了就照定住的順序，新出現的照欄排在最後
        var found = sample.Programs
            .Where(p => keyword.Length == 0
                || p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || p.Path.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || signers.GetValueOrDefault(p.Path, string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var byText = sort is "name" or "signer" or "rule";
        var ordered = (byText
                ? (descending ? found.OrderByDescending(p => (string)Value(p, sort), StringComparer.CurrentCulture) : found.OrderBy(p => (string)Value(p, sort), StringComparer.CurrentCulture))
                : (descending ? found.OrderByDescending(p => (double)Value(p, sort)) : found.OrderBy(p => (double)Value(p, sort))))
            .ThenBy(p => p.Name, StringComparer.CurrentCulture)
            .ToList();
        var held = (order ?? []).Select((key, i) => (key, i)).ToDictionary(one => one.key, one => one.i);
        ordered = order is null ? ordered : [.. ordered.OrderBy(p => held.GetValueOrDefault(p.Key, int.MaxValue))];

        // 選中那一支：一定是清單上看得到的那一列（選過的還在清單上就留著；不在了、被搜尋濾掉了就落到清單第一列），工具列的動作對著它
        var chosen = ordered.FirstOrDefault(p => p.Key == picked) ?? ordered.FirstOrDefault();
        var mine = chosen is null ? string.Empty : chosen.Key;

        // 沒有回應的每一支：卡最久的在上；它還在動多少；同一時間各項吃最多的是誰；緩和要暫停的是 CPU 吃最多、不是系統關鍵、不是 HelixStudio 自己的那一支
        var hangs = sample.Programs.Where(p => p.Hung).OrderBy(p => p.HungSince).Select(p =>
        {
            var others = sample.Programs.Where(one => one.Key != p.Key).ToList();
            var tops = new[]
                {
                    (Word: words["top.cpu"], Who: others.MaxBy(one => one.Cpu), Show: (Func<ProgramItem, string>)(one => Format("%", one.Cpu))),
                    (Word: words["top.up"], Who: netShown ? others.MaxBy(one => one.Up ?? 0) : null, Show: one => Format("KB/s", (one.Up ?? 0) / 1024.0)),
                    (Word: words["top.disk"], Who: others.MaxBy(one => one.Disk), Show: one => Format("KB/s", one.Disk / 1024.0)),
                    (Word: words["top.mem"], Who: others.MaxBy(one => one.Mem), Show: one => Format("MB", one.Mem)),
                }
                .Where(one => one.Who is not null)
                .GroupBy(one => one.Who!.Key)
                .Select(group => string.Format(words["top.one"], group.First().Who!.Name, string.Join("、", group.Select(one => $"{one.Word} {one.Show(one.Who!)}"))));
            var hog = others.Where(one => !one.Critical && one.Session != 0 && one.Key != self).MaxBy(one => one.Cpu);
            var seconds = (int)Math.Round((sample.At - p.HungSince!.Value).TotalSeconds) + HungDelay;
            var easedOther = eased.GetValueOrDefault(p.Key);

            return new HangItem
            {
                Key = p.Key,
                Name = p.Name,
                Since = p.HungSince!.Value.AddSeconds(-HungDelay).ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Now = string.Format(words["hang.now"], Span(TimeSpan.FromSeconds(seconds), words), Format("%", p.Cpu), Format("KB/s", p.Disk / 1024.0)),
                Tops = string.Join(" · ", tops),
                Eased = easedOther is not null,
                HogKey = easedOther ?? hog?.Key ?? string.Empty,
                HogName = easedOther is not null ? sample.Programs.FirstOrDefault(one => one.Key == easedOther)?.Name ?? easedOther : hog?.Name ?? string.Empty,
            };
        }).ToList();

        // 事件表：還沒恢復的無回應在最上，接著記下來的，新的在前；選中那一支的事件反白
        var events = hangs
            .Select(one => new EventRow { At = one.Since, Name = one.Name, Kind = words["event.hang"], Detail = one.Now, Danger = true, Mine = one.Key == mine })
            .Concat(rows
                .Where(row => row.type == "event")
                .OrderBy(row => row.sort)
                .Select(row => new EventRow
                {
                    At = $"{row.at_text}",
                    Name = $"{row.name}",
                    Kind = words.GetValueOrDefault($"event.{row.event_type}", $"{row.event_type}"),
                    Detail = Detail(row, words),
                    Danger = $"{row.event_type}" is "crash" or "hang",
                    Mine = chosen is not null && $"{row.exe_path}".Equals(chosen.Path.Length > 0 ? chosen.Path : chosen.Name, StringComparison.OrdinalIgnoreCase),
                }))
            .ToList();

        // 右邊的分頁：連線（每一條的速度）、開的埠、這一支的事件、載入的模組
        var pidSet = chosen?.Pids.ToHashSet() ?? [];
        var linkRates = (links ?? new Dictionary<(int Pid, string Remote), (long Down, long Up)>())
            .Where(one => pidSet.Contains(one.Key.Pid))
            .GroupBy(one => one.Key.Remote)
            .ToDictionary(group => group.Key, group => (Down: group.Sum(one => one.Value.Down), Up: group.Sum(one => one.Value.Up)));
        var connections = sockets.Where(one => pidSet.Contains(one.Pid) && !one.Listening && one.Remote.Length > 0).Select(one => one.Remote)
            .Concat(linkRates.Keys)
            .Distinct()
            .Select(remote => (Remote: remote, Rate: linkRates.GetValueOrDefault(remote)))
            .OrderByDescending(one => one.Rate.Down + one.Rate.Up)
            .ThenBy(one => one.Remote, StringComparer.Ordinal)
            .Select(one => new LineItem { A = one.Remote, B = netShown ? Format("KB/s", one.Rate.Down / 1024.0) : Blank, C = netShown ? Format("KB/s", one.Rate.Up / 1024.0) : Blank })
            .ToList();
        var ports = chosen is null ? [] : sockets.Where(one => pidSet.Contains(one.Pid) && one.Listening)
            .DistinctBy(one => (one.Local, one.Protocol))
            .OrderBy(one => one.Local, StringComparer.Ordinal)
            .Select(one => listenSince.GetValueOrDefault((chosen.Key, one.Protocol, one.Local)) is DateTime since
                ? new LineItem { A = one.Local, B = one.Protocol, C = string.Format(words["port.since"], since.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture)) }
                : new LineItem { A = one.Local, B = one.Protocol, C = words["port.before"] })
            .ToList();

        // 走勢：近 10 分鐘，最高的那一點頂到上緣，寬照畫面
        PointCollection Points(double[] values)
        {
            var max = Math.Max(values.DefaultIfEmpty(0).Max(), 1e-9);
            var step = values.Length > 1 ? width / (values.Length - 1) : 0;
            var points = new PointCollection(values.Select((v, i) => new System.Windows.Point(i * step, ChartHeight - 1 - (v / max * (ChartHeight - 2)))));
            points.Freeze();
            return points;
        }

        var down = traces.GetValueOrDefault("down", []);
        var up = traces.GetValueOrDefault("up", []);

        return new BoardView
        {
            Admin = admin,
            NetFault = netFault,
            Since = rows.Where(row => row.type == "since").Select(row => row.at_text).FirstOrDefault() ?? sample.At.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture),
            Columns = [.. Metrics.Select(one => new ColumnItem { Key = one.Key, Label = words[$"metric.{one.Key}"], Shown = shown.Contains(one) })],
            ShownCount = shown.Count,
            MetricCount = Metrics.Count,
            Heads = [.. shown.Select(one => new HeadItem { Key = one.Key, Text = Arrow(words[$"metric.{one.Key}"], one.Key, sort, descending), Width = one.Width, Right = one.Unit != "text", On = sort == one.Key })],
            NameHead = Arrow(words["metric.name"], "name", sort, descending),
            NameOn = sort == "name",
            Rows = [.. ordered.Select(p => new ProgramRow
            {
                Key = p.Key,
                Name = p.Name,
                Path = p.Path,
                Hung = p.Hung,
                Cells = [.. shown.Select(m => new CellItem { Text = Text(p, m), Width = m.Width, Right = m.Unit != "text", Mono = m.Unit != "text" })],
            })],
            Picked = chosen?.Key ?? string.Empty,
            Chosen = chosen is null ? -1 : ordered.FindIndex(p => p.Key == chosen.Key),
            Detail = chosen is null ? null : new DetailItem
            {
                Name = chosen.Name,
                Exe = chosen.Path.Length > 0 ? System.IO.Path.GetFileName(chosen.Path) : chosen.Name,
                Priority = chosen.Priority,
                PriorityName = words.GetValueOrDefault($"priority.{chosen.Priority}", $"{chosen.Priority}"),
                Paused = paused.Contains(chosen.Key),
                Hung = chosen.Hung,
                Path = chosen.Path.Length > 0 ? chosen.Path : Blank,
                Signer = Signer(chosen) is { Length: > 0 } signer ? signer : Blank,
                Version = chosen.Version.Length > 0 ? chosen.Version : Blank,
                Started = chosen.Start?.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture) ?? Blank,
                Autorun = string.Join("；", startups.Where(one => one.Path.Equals(chosen.Path, StringComparison.OrdinalIgnoreCase))
                    .Select(one => $"{words.GetValueOrDefault($"source.{one.Source}", one.Source)} {one.Location}")) is { Length: > 0 } where ? where : Blank,
                CpuPoints = Points(traces.GetValueOrDefault("cpu", [])),
                MemPoints = Points(traces.GetValueOrDefault("mem", [])),
                NetPoints = netShown ? Points([.. down.Zip(up, (a, b) => a + b)]) : Points([]),
                CpuNow = Format("%", chosen.Cpu),
                MemNow = Format("MB", chosen.Mem),
                NetNow = netShown ? Format("KB/s", ((chosen.Down ?? 0) + (chosen.Up ?? 0)) / 1024.0) : Blank,
                Tab = tab,
                Connections = connections,
                Ports = ports,
                Events = [.. events.Where(one => one.Mine).Select(one => new LineItem { A = one.At, B = one.Kind, C = one.Detail, Danger = one.Danger })],
                Modules = [.. modules.Select(one => new LineItem { A = one.Name, B = one.Version.Length > 0 ? one.Version : Blank })],
            },
            Events = events,
            Hangs = hangs,
        };
    }

    /// <summary>限制對話框：上傳、不准連網路、CPU、記憶體現在設的值，與每一項現在、近 10 分鐘最高的值</summary>
    /// <param name="program">那一支程式</param>
    /// <param name="rows">vw_monitor 的每一列</param>
    /// <param name="traces">那一支近 10 分鐘的走勢</param>
    /// <param name="sockets">這一刻每一條連線</param>
    /// <param name="netShown">量得到網路</param>
    internal static LimitView Limit(ProgramItem program, IReadOnlyList<vw_monitor> rows, IReadOnlyDictionary<string, double[]> traces, IReadOnlyList<WindowsStore.SocketItem> sockets, bool netShown)
    {
        var limit = rows.FirstOrDefault(row => row.type == "limit" && $"{row.exe_path}".Equals(program.Path, StringComparison.OrdinalIgnoreCase));

        return new LimitView
        {
            Key = program.Key,
            Name = program.Name,
            Up = limit?.up_kbps is int up ? (up / 1024.0).ToString("0.##", CultureInfo.InvariantCulture) : string.Empty,
            Block = limit?.block == true,
            Cpu = limit?.cpu_pct is int cpu ? $"{cpu}" : string.Empty,
            Mem = limit?.mem_mb is int mem ? $"{mem}" : string.Empty,
            UpNow = netShown ? Format("KB/s", (program.Up ?? 0) / 1024.0) : Blank,
            UpPeak = netShown ? Format("KB/s", traces.GetValueOrDefault("up", []).DefaultIfEmpty(0).Max() / 1024.0) : Blank,
            CpuNow = Format("%", program.Cpu),
            CpuPeak = Format("%", traces.GetValueOrDefault("cpu", []).DefaultIfEmpty(0).Max()),
            MemNow = Format("MB", program.Mem),
            MemPeak = Format("MB", traces.GetValueOrDefault("mem", []).DefaultIfEmpty(0).Max()),
            Links = sockets.Count(one => program.Pids.Contains(one.Pid) && !one.Listening && one.Remote.Length > 0),
        };
    }

    #endregion

    #region 基本 CRUD 操作

    /// <summary>見過的程式與開機自動執行：第一次用這支工具時把當下的全部記成見過（不記事件）；之後新出現的記成見過並記一筆事件</summary>
    /// <param name="programs">這一刻每一支程式</param>
    /// <param name="startups">這一刻每一個開機自動執行；這一次沒有掃是空的</param>
    /// <param name="rows">vw_monitor 的每一列</param>
    /// <param name="now">這一刻</param>
    internal static List<monitor> Seen(IReadOnlyList<ProgramItem> programs, IReadOnlyList<WindowsStore.StartupItem> startups, IReadOnlyList<vw_monitor> rows, DateTime now)
    {
        var seen = rows.Where(row => row.type == "seen").Select(row => $"{row.exe_path}".ToLowerInvariant()).ToHashSet();
        var known = rows.Where(row => row.type == "startup").Select(row => ($"{row.exe_path}".ToLowerInvariant(), $"{row.source}", $"{row.location}")).ToHashSet();
        var utc = now.ToUniversalTime();

        // 新出現的程式與開機自動執行；一筆都還沒見過的是第一次用，只記見過不記事件
        var fresh = programs.Where(p => p.Path.Length > 0 && !seen.Contains(p.Path.ToLowerInvariant())).DistinctBy(p => p.Path.ToLowerInvariant()).ToList();
        var added = startups.Where(one => !known.Contains((one.Path.ToLowerInvariant(), one.Source, one.Location))).ToList();

        return
        [
            .. fresh.Select(p => new monitor { type = "seen", exe_path = p.Path, created_at = utc, updated_at = utc }),
            .. fresh.Where(_ => seen.Count > 0).Select(p => new monitor { type = "first", exe_path = p.Path, name = p.Name, happened_at = utc, created_at = utc, updated_at = utc }),
            .. added.Select(one => new monitor { type = "startup", exe_path = one.Path, source = one.Source, location = one.Location, created_at = utc, updated_at = utc }),
            .. added.Where(_ => known.Count > 0).Select(one => new monitor { type = "autorun", exe_path = one.Path, name = System.IO.Path.GetFileNameWithoutExtension(one.Path), source = one.Source, location = one.Location, happened_at = utc, created_at = utc, updated_at = utc }),
        ];
    }

    /// <summary>這一秒的事：恢復回應（或沒回應就結束）的卡了多久、新開始聽的埠、Windows 事件記錄裡新的閃退與無回應</summary>
    /// <param name="sample">這一次量到的</param>
    /// <param name="previous">上一次量到的（恢復的那一支這一次可能已經不在）</param>
    /// <param name="opened">新開始聽的埠：程式、協定、位址</param>
    /// <param name="crashes">Windows 事件記錄裡新的那幾筆</param>
    /// <param name="rows">vw_monitor 的每一列（.NET 閃退只有檔名，照見過的補成路徑）</param>
    internal static List<monitor> Happened(SampleView sample, SampleView? previous, IReadOnlyList<(ProgramItem Program, string Protocol, string Address)> opened,
        IReadOnlyList<WindowsStore.CrashItem> crashes, IReadOnlyList<vw_monitor> rows)
    {
        var utc = sample.At.ToUniversalTime();
        var everyone = sample.Programs.Concat(previous?.Programs ?? []).GroupBy(p => p.Key).ToDictionary(group => group.Key, group => group.First());
        var seen = rows.Where(row => row.type == "seen").Select(row => $"{row.exe_path}").ToList();

        return
        [
            .. sample.Recovered.Where(one => everyone.ContainsKey(one.Key)).Select(one => new monitor
            {
                type = "hang",
                exe_path = everyone[one.Key].Path.Length > 0 ? everyone[one.Key].Path : everyone[one.Key].Name,
                name = everyone[one.Key].Name,
                seconds = one.Seconds,
                happened_at = utc,
                created_at = utc,
                updated_at = utc,
            }),
            .. opened.Select(one => new monitor { type = "listen", exe_path = one.Program.Path.Length > 0 ? one.Program.Path : one.Program.Name, name = one.Program.Name, protocol = one.Protocol, address = one.Address, happened_at = utc, created_at = utc, updated_at = utc }),
            .. crashes.Select(one => new monitor
            {
                type = one.Kind,
                exe_path = one.ExePath.Contains('\\') ? one.ExePath : seen.FirstOrDefault(path => System.IO.Path.GetFileName(path).Equals(one.ExePath, StringComparison.OrdinalIgnoreCase)) ?? one.ExePath,
                name = one.Name,
                record_id = one.RecordId,
                code = one.Code.Length > 0 ? one.Code : null,
                module = one.Module.Length > 0 ? one.Module : null,
                happened_at = one.At.ToUniversalTime(),
                created_at = utc,
                updated_at = utc,
            }),
        ];
    }

    /// <summary>在這台做的一件事：改優先順序、暫停、繼續、結束、緩和、取消緩和</summary>
    /// <param name="type">priority、suspend、resume、end、ease、unease</param>
    /// <param name="program">對著哪一支</param>
    /// <param name="from">改之前的優先順序；沒改是 null</param>
    /// <param name="to">改之後的優先順序；沒改是 null</param>
    /// <param name="other">緩和時被暫停、被繼續的那一支的路徑</param>
    /// <param name="now">這一刻</param>
    internal static monitor Acted(string type, ProgramItem program, int? from, int? to, string? other, DateTime now) =>
        new()
        {
            type = type,
            exe_path = program.Path.Length > 0 ? program.Path : program.Name,
            name = program.Name,
            priority_from = from,
            priority_to = to,
            other_path = other,
            happened_at = now.ToUniversalTime(),
            created_at = now.ToUniversalTime(),
            updated_at = now.ToUniversalTime(),
        };

    /// <summary>限制對話框按套用：檢查填的值；都看得懂就是要記的那一列（四項都空就是解除），看不懂就是原因</summary>
    /// <param name="program">對著哪一支</param>
    /// <param name="up">上傳上限 MB/s 的字</param>
    /// <param name="block">不准連網路</param>
    /// <param name="cpu">CPU 上限 % 的字</param>
    /// <param name="mem">記憶體上限 MB 的字</param>
    /// <param name="words">畫面上的字</param>
    /// <param name="now">這一刻</param>
    internal static (string Fault, monitor? Row) Limited(ProgramItem program, string up, bool block, string cpu, string mem, IReadOnlyDictionary<string, string> words, DateTime now)
    {
        var upOk = double.TryParse(up.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var u) && u > 0;
        var cpuOk = int.TryParse(cpu.Trim(), out var c) && c > 0 && c <= 100;
        var memOk = int.TryParse(mem.Trim(), out var m) && m > 0;
        var fault = string.Join("；", new[]
        {
            up.Trim().Length > 0 && !upOk ? words["fault.up"] : string.Empty,
            cpu.Trim().Length > 0 && !cpuOk ? words["fault.cpu"] : string.Empty,
            mem.Trim().Length > 0 && !memOk ? words["fault.mem"] : string.Empty,
        }.Where(one => one.Length > 0));

        return fault.Length > 0
            ? (fault, null)
            : (string.Empty, new monitor
            {
                type = "limit",
                exe_path = program.Path,
                name = program.Name,
                up_kbps = upOk ? (int)Math.Round(u * 1024) : null,
                cpu_pct = cpuOk ? c : null,
                mem_mb = memOk ? m : null,
                block = block,
                happened_at = now.ToUniversalTime(),
                created_at = now.ToUniversalTime(),
                updated_at = now.ToUniversalTime(),
            });
    }

    /// <summary>限制套用的結果：做不到的那幾項保持原本的值，做到的照新的值；回傳每一項做不到的原因（寫上是哪一項）與要記的那一列（和原本一樣是 null）</summary>
    /// <param name="row">照填的值做出來的那一列</param>
    /// <param name="before">原本的限制；沒設過是 null</param>
    /// <param name="upFault">上傳上限做不到的原因；沒做或做到是空字串</param>
    /// <param name="blockFault">不准連網路做不到的原因</param>
    /// <param name="jobFault">CPU、記憶體上限做不到的原因</param>
    /// <param name="admin">有沒有系統管理員權限（沒有時，上傳上限與不准連網路做不到的原因後面寫上怎麼解決）</param>
    /// <param name="words">畫面上的字</param>
    internal static (string Fault, monitor? Row) Applied(monitor row, vw_monitor? before, string upFault, string blockFault, string jobFault, bool admin, IReadOnlyDictionary<string, string> words)
    {
        row.up_kbps = upFault.Length > 0 ? before?.up_kbps : row.up_kbps;
        row.block = blockFault.Length > 0 ? before?.block == true : row.block;
        row.cpu_pct = jobFault.Length > 0 ? before?.cpu_pct : row.cpu_pct;
        row.mem_mb = jobFault.Length > 0 ? before?.mem_mb : row.mem_mb;
        var changed = row.up_kbps != before?.up_kbps || (row.block == true) != (before?.block == true) || row.cpu_pct != before?.cpu_pct || row.mem_mb != before?.mem_mb;
        var fault = string.Join("；", new[] { (Part: words["limit.up"], Why: upFault, Admin: !admin), (Part: words["limit.block"], Why: blockFault, Admin: !admin), (Part: words["limit.job"], Why: jobFault, Admin: false) }
            .Where(one => one.Why.Length > 0)
            .Select(one => string.Format(words["fault.part"], one.Part, one.Why.Trim()) + (one.Admin ? words["fault.admin"] : string.Empty)));

        return (fault, changed ? row : null);
    }

    /// <summary>勾一個量測項目：排成欄的那一份整列帶過來換成新的（沒存過就是新的一列）</summary>
    /// <param name="rows">vw_monitor 的每一列</param>
    /// <param name="key">勾的那一項</param>
    /// <param name="on">勾上或拿掉</param>
    /// <param name="now">這一刻</param>
    internal static monitor Columned(IReadOnlyList<vw_monitor> rows, string key, bool on, DateTime now)
    {
        var row = rows.FirstOrDefault(one => one.type == "columns");
        var held = row?.columns ?? [.. Metrics.Where(one => one.Shown).Select(one => one.Key)];
        var next = on ? [.. held, key] : held.Where(one => one != key).ToList();

        // 照量測項目表的先後排
        return new monitor
        {
            id = row?.id ?? 0,
            type = "columns",
            columns = [.. Metrics.Select(one => one.Key).Where(next.Contains)],
            created_at = now.ToUniversalTime(),
            updated_at = now.ToUniversalTime(),
        };
    }

    #endregion

    #region 字

    /// <summary>限制欄的字：暫停中、優先順序（不是一般才寫）、上傳、擋網路、CPU、記憶體，有幾樣列幾樣；都沒有是破折號</summary>
    private static string Rule(ProgramItem p, vw_monitor? limit, IReadOnlySet<string> paused, IReadOnlyDictionary<string, string> words) =>
        string.Join(" · ", new[]
        {
            paused.Contains(p.Key) ? words["rule.paused"] : string.Empty,
            p.Priority is not (32 or 0) ? string.Format(words["rule.priority"], words.GetValueOrDefault($"priority.{p.Priority}", $"{p.Priority}")) : string.Empty,
            Limits(limit?.up_kbps, limit?.block, limit?.cpu_pct, limit?.mem_mb, words),
        }.Where(one => one.Length > 0)) is { Length: > 0 } text ? text : Blank;

    /// <summary>上傳、擋網路、CPU、記憶體的上限寫成一句；都沒有是空字串</summary>
    private static string Limits(int? up, bool? block, int? cpu, int? mem, IReadOnlyDictionary<string, string> words) =>
        string.Join(" · ", new[]
        {
            up is int u ? string.Format(words["rule.up"], (u / 1024.0).ToString("0.##", CultureInfo.InvariantCulture)) : string.Empty,
            block == true ? words["rule.block"] : string.Empty,
            cpu is int c ? string.Format(words["rule.cpu"], c) : string.Empty,
            mem is int m ? string.Format(words["rule.mem"], m) : string.Empty,
        }.Where(one => one.Length > 0));

    /// <summary>事件表「內容」那一格：照事件的種類把值填進句型</summary>
    private static string Detail(vw_monitor row, IReadOnlyDictionary<string, string> words)
    {
        string Priority(int? value) => words.GetValueOrDefault($"priority.{value}", $"{value}");
        string Other() => System.IO.Path.GetFileNameWithoutExtension(row.other_path ?? string.Empty);

        return $"{row.event_type}" switch
        {
            "crash" => string.Join(" · ", new[] { row.code, row.module }.Where(one => !string.IsNullOrEmpty(one))),
            "hang" => row.seconds is int s ? string.Format(words["detail.hang"], Span(TimeSpan.FromSeconds(s), words)) : words["detail.hangEnded"],
            "end" => words["detail.end"],
            "priority" => $"{Priority(row.priority_from)} → {Priority(row.priority_to)}",
            "suspend" => words["detail.suspend"],
            "resume" => words["detail.resume"],
            "ease" => string.Format(words["detail.ease"], Priority(row.priority_to), Other()),
            "unease" => string.Format(words["detail.unease"], Priority(row.priority_to), Other()),
            "limit" => Limits(row.up_kbps, row.block, row.cpu_pct, row.mem_mb, words) is { Length: > 0 } text ? text : words["detail.unlimit"],
            "first" => $"{row.exe_path}",
            "autorun" => $"{words.GetValueOrDefault($"source.{row.source}", $"{row.source}")} {row.location}",
            "listen" => $"{row.protocol} {row.address}",
            _ => string.Empty,
        };
    }

    /// <summary>一個值寫成字：% 一位小數；MB 千分位；KB/s 到 1 MB/s 換成 MB/s，一位小數；其他是個數</summary>
    private static string Format(string unit, double value) => unit switch
    {
        "%" => value.ToString("0.0", CultureInfo.InvariantCulture) + "%",
        "MB" => (value / 1024 / 1024).ToString("#,##0", CultureInfo.InvariantCulture) + " MB",
        "KB/s" => value >= 1024 ? (value / 1024).ToString("0.0", CultureInfo.InvariantCulture) + " MB/s" : value.ToString("0.0", CultureInfo.InvariantCulture) + " KB/s",
        _ => value.ToString("#,##0", CultureInfo.InvariantCulture),
    };

    /// <summary>一段時間：時 分、分 秒、秒</summary>
    private static string Span(TimeSpan span, IReadOnlyDictionary<string, string> words) =>
        span.TotalHours >= 1 ? string.Format(words["span.h"], (int)span.TotalHours, span.Minutes)
        : span.TotalMinutes >= 1 ? string.Format(words["span.m"], span.Minutes, span.Seconds)
        : string.Format(words["span.s"], span.Seconds);

    /// <summary>欄名：排序的那一欄帶箭頭</summary>
    private static string Arrow(string label, string key, string sort, bool descending) =>
        key == sort ? $"{label} {(descending ? "↓" : "↑")}" : label;

    #endregion

    #region 對照

    /// <summary>Windows 判定沒有回應要等幾秒（沒處理訊息 5 秒）</summary>
    private const int HungDelay = 5;

    /// <summary>走勢的高</summary>
    public const double ChartHeight = 28;

    /// <summary>沒有值的格子</summary>
    private const string Blank = "—";

    /// <summary>量測項目：清單能排成欄的每一項；欄寬照最長的值（等寬字約字級 × 0.6，再加欄間距）；欄名在 Words 的 metric.{Key}</summary>
    internal static readonly IReadOnlyList<MetricItem> Metrics =
    [
        new() { Key = "cpu", Unit = "%", Width = 56, Shown = true },
        new() { Key = "mem", Unit = "MB", Width = 80, Shown = true },
        new() { Key = "down", Unit = "KB/s", Width = 88, Shown = true },
        new() { Key = "up", Unit = "KB/s", Width = 88, Shown = true },
        new() { Key = "disk", Unit = "KB/s", Width = 88, Shown = false },
        new() { Key = "run", Unit = "span", Width = 88, Shown = false },
        new() { Key = "crash", Unit = string.Empty, Width = 48, Shown = true },
        new() { Key = "signer", Unit = "text", Width = 128, Shown = true },
        new() { Key = "rule", Unit = "text", Width = 128, Shown = true },
        new() { Key = "gpu", Unit = "%", Width = 56, Shown = false },
        new() { Key = "threads", Unit = string.Empty, Width = 56, Shown = false },
        new() { Key = "handles", Unit = string.Empty, Width = 64, Shown = false },
    ];

    #endregion

    #region 模型

    /// <summary>一個量測項目</summary>
    public class MetricItem
    {
        public string Key { get; set; } = string.Empty;

        /// <summary>%、MB、KB/s、span、text 或空字串（個數）</summary>
        public string Unit { get; set; } = string.Empty;

        public double Width { get; set; }

        /// <summary>沒存過欄位設定時排成欄</summary>
        public bool Shown { get; set; }
    }

    /// <summary>一支程式（同一個路徑的程序併在一起）這一刻的值</summary>
    public class ProgramItem
    {
        /// <summary>路徑轉小寫；沒有路徑的是 name:名字</summary>
        public string Key { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;

        public List<int> Pids { get; set; } = [];

        /// <summary>CPU %（佔全部核心）</summary>
        public double Cpu { get; set; }

        /// <summary>記憶體（私人位元組）</summary>
        public long Mem { get; set; }

        /// <summary>下載位元組 / 秒；量不到是 null</summary>
        public double? Down { get; set; }

        /// <summary>上傳位元組 / 秒；量不到是 null</summary>
        public double? Up { get; set; }

        /// <summary>磁碟讀寫位元組 / 秒</summary>
        public double Disk { get; set; }

        public double Gpu { get; set; }

        public int Threads { get; set; }

        public int Handles { get; set; }

        public DateTime? Start { get; set; }

        /// <summary>Windows 的優先順序類別值（最早那一個程序的）</summary>
        public int Priority { get; set; }

        public int Session { get; set; }

        public bool Critical { get; set; }

        public bool Hung { get; set; }

        /// <summary>從什麼時候開始沒有回應（第一次看到的那一刻）</summary>
        public DateTime? HungSince { get; set; }
    }

    /// <summary>量一次的結果</summary>
    public class SampleView
    {
        public DateTime At { get; set; }

        public List<ProgramItem> Programs { get; set; } = [];

        /// <summary>這一次每一個程序的累計 CPU 時間與讀寫位元組（下一次算速率用）</summary>
        public Dictionary<int, (long Cpu, ulong Io)> Before { get; set; } = [];

        /// <summary>每一支沒有回應的程式從什麼時候開始</summary>
        public Dictionary<string, DateTime> HungSince { get; set; } = [];

        /// <summary>這一秒恢復回應（或沒回應就結束）的程式</summary>
        public List<RecoveredItem> Recovered { get; set; } = [];
    }

    /// <summary>恢復回應（或沒回應就結束）的一支</summary>
    public class RecoveredItem
    {
        public string Key { get; set; } = string.Empty;

        /// <summary>卡了幾秒</summary>
        public int Seconds { get; set; }
    }

    /// <summary>監控台</summary>
    public class BoardView
    {
        public bool Admin { get; set; }

        /// <summary>網路量不到的原因；量得到是空字串</summary>
        public string NetFault { get; set; } = string.Empty;

        /// <summary>從什麼時候開始記錄</summary>
        public string Since { get; set; } = string.Empty;

        /// <summary>欄位清單：每一個量測項目、有沒有排成欄</summary>
        public List<ColumnItem> Columns { get; set; } = [];

        public int ShownCount { get; set; }

        public int MetricCount { get; set; }

        /// <summary>程式那一欄的欄名（排序時帶箭頭）</summary>
        public string NameHead { get; set; } = string.Empty;

        public bool NameOn { get; set; }

        /// <summary>程式以外排成欄的欄名</summary>
        public List<HeadItem> Heads { get; set; } = [];

        public List<ProgramRow> Rows { get; set; } = [];

        /// <summary>選中那一支的 Key</summary>
        public string Picked { get; set; } = string.Empty;

        /// <summary>選中那一支在清單的第幾列；清單是空的是 -1</summary>
        public int Chosen { get; set; }

        /// <summary>右邊選中那一支的明細；沒有程式是 null</summary>
        public DetailItem? Detail { get; set; }

        public List<EventRow> Events { get; set; } = [];

        /// <summary>沒有回應的每一支：卡最久的在上</summary>
        public List<HangItem> Hangs { get; set; } = [];

        /// <summary>最後在這台做的那件事做不到的原因；都做到是空字串（Service 填）</summary>
        public string Fault { get; set; } = string.Empty;
    }

    /// <summary>欄位清單的一項</summary>
    public class ColumnItem
    {
        public string Key { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public bool Shown { get; set; }
    }

    /// <summary>一個欄名</summary>
    public class HeadItem
    {
        public string Key { get; set; } = string.Empty;

        /// <summary>欄名；排序的那一欄帶箭頭</summary>
        public string Text { get; set; } = string.Empty;

        public double Width { get; set; }

        public bool Right { get; set; }

        /// <summary>照這一欄排</summary>
        public bool On { get; set; }
    }

    /// <summary>清單的一列：一支程式</summary>
    public class ProgramRow
    {
        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public bool Hung { get; set; }

        public List<CellItem> Cells { get; set; } = [];
    }

    /// <summary>一格</summary>
    public class CellItem
    {
        public string Text { get; set; } = string.Empty;

        public double Width { get; set; }

        public bool Right { get; set; }

        /// <summary>數字用等寬字</summary>
        public bool Mono { get; set; }
    }

    /// <summary>右邊選中那一支</summary>
    public class DetailItem
    {
        public string Name { get; set; } = string.Empty;

        public string Exe { get; set; } = string.Empty;

        /// <summary>Windows 的優先順序類別值</summary>
        public int Priority { get; set; }

        public string PriorityName { get; set; } = string.Empty;

        /// <summary>在這台暫停中</summary>
        public bool Paused { get; set; }

        public bool Hung { get; set; }

        public string Path { get; set; } = string.Empty;

        public string Signer { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;

        /// <summary>這一次啟動的時刻</summary>
        public string Started { get; set; } = string.Empty;

        /// <summary>開機自動執行：從哪裡、哪一項</summary>
        public string Autorun { get; set; } = string.Empty;

        public PointCollection CpuPoints { get; set; } = [];

        public PointCollection MemPoints { get; set; } = [];

        public PointCollection NetPoints { get; set; } = [];

        public string CpuNow { get; set; } = string.Empty;

        public string MemNow { get; set; } = string.Empty;

        public string NetNow { get; set; } = string.Empty;

        /// <summary>看的那一個分頁：conn、port、event、module</summary>
        public string Tab { get; set; } = string.Empty;

        /// <summary>連線：遠端位址、下載、上傳</summary>
        public List<LineItem> Connections { get; set; } = [];

        /// <summary>開的埠：位址、協定、從什麼時候</summary>
        public List<LineItem> Ports { get; set; } = [];

        /// <summary>這一支的事件：時間、事件、內容</summary>
        public List<LineItem> Events { get; set; } = [];

        /// <summary>載入的模組：模組、版本</summary>
        public List<LineItem> Modules { get; set; } = [];
    }

    /// <summary>右邊分頁的一列（三格）</summary>
    public class LineItem
    {
        public string A { get; set; } = string.Empty;

        public string B { get; set; } = string.Empty;

        public string C { get; set; } = string.Empty;

        /// <summary>閃退、無回應：紅字</summary>
        public bool Danger { get; set; }
    }

    /// <summary>事件表的一列</summary>
    public class EventRow
    {
        public string At { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string Detail { get; set; } = string.Empty;

        /// <summary>閃退、無回應：紅字</summary>
        public bool Danger { get; set; }

        /// <summary>選中那一支的事件</summary>
        public bool Mine { get; set; }
    }

    /// <summary>一支沒有回應的程式（橫幅、跳到最上面的小視窗）</summary>
    public class HangItem
    {
        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>從什麼時候開始（Windows 判定前的 5 秒算進去）</summary>
        public string Since { get; set; } = string.Empty;

        /// <summary>已經多久、它這一刻的 CPU 與磁碟讀寫</summary>
        public string Now { get; set; } = string.Empty;

        /// <summary>同一時間各項吃最多的是誰</summary>
        public string Tops { get; set; } = string.Empty;

        /// <summary>在這台緩和中</summary>
        public bool Eased { get; set; }

        /// <summary>緩和要暫停（或已經暫停）的那一支；沒有可以暫停的是空字串</summary>
        public string HogKey { get; set; } = string.Empty;

        public string HogName { get; set; } = string.Empty;
    }

    /// <summary>限制對話框</summary>
    public class LimitView
    {
        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>上傳上限 MB/s；不限是空字串</summary>
        public string Up { get; set; } = string.Empty;

        public bool Block { get; set; }

        /// <summary>CPU 上限 %；不限是空字串</summary>
        public string Cpu { get; set; } = string.Empty;

        /// <summary>記憶體上限 MB；不限是空字串</summary>
        public string Mem { get; set; } = string.Empty;

        public string UpNow { get; set; } = string.Empty;

        public string UpPeak { get; set; } = string.Empty;

        public string CpuNow { get; set; } = string.Empty;

        public string CpuPeak { get; set; } = string.Empty;

        public string MemNow { get; set; } = string.Empty;

        public string MemPeak { get; set; } = string.Empty;

        /// <summary>現在幾條連線</summary>
        public int Links { get; set; }
    }

    #endregion
}
