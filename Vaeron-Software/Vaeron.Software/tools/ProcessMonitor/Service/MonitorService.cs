using System.Collections.Concurrent;
using Models.HelixStudio;
using ProcessMonitor.Logic;
using ProcessMonitor.Store;

namespace ProcessMonitor.Service;

/// <summary>程式監控的每一個動作</summary>
/// <remarks>
/// 一個動作一支方法：問 Windows、讀 vw_monitor → MonitorLogic 算 → 寫 monitor → 交回監控台。
/// 量到的值、走勢、暫停中與緩和中的程式只在這一份 HelixStudio 開著的期間留著；畫面在背景執行緒呼叫，每一支都先拿鎖。
/// </remarks>
internal sealed class MonitorService : IDisposable
{
    /// <summary>近 10 分鐘：每秒一點</summary>
    private const int TraceLength = 600;

    private readonly object gate = new();

    private readonly MonitorStore store;

    private readonly WindowsStore windows = new();

    /// <summary>網路量測；沒有系統管理員權限是 null</summary>
    private readonly NetStore? net;

    private readonly bool admin;

    /// <summary>畫面上的字（Board.xaml 的 Words）</summary>
    private readonly IReadOnlyDictionary<string, string> words;

    /// <summary>HelixStudio 自己的 Key</summary>
    private readonly string self = (Environment.ProcessPath ?? string.Empty).ToLowerInvariant();

    /// <summary>vw_monitor 的每一列；寫過就重讀</summary>
    private List<vw_monitor> rows;

    private MonitorLogic.SampleView? last;

    private List<WindowsStore.SocketItem> sockets = [];

    private Dictionary<(int Pid, string Remote), (long Down, long Up)>? links;

    private List<WindowsStore.StartupItem> startups = [];

    private List<(string Name, string Version)> modules = [];

    private readonly Dictionary<string, (string Name, string Version)> files = [];

    private readonly ConcurrentDictionary<string, string> signers = new(StringComparer.OrdinalIgnoreCase);

    private readonly BlockingCollection<string> unsigned = [];

    private readonly Dictionary<string, Dictionary<string, List<double>>> traces = [];

    private readonly Dictionary<(string Key, string Protocol, string Local), DateTime?> listenSince = [];

    private readonly HashSet<string> paused = [];

    /// <summary>緩和中：沒有回應的那一支 → 被暫停的那一支、它原本的優先順序</summary>
    private readonly Dictionary<string, (string Other, int Before)> eased = [];

    /// <summary>已經放進 CPU、記憶體上限 Job Object 的程序</summary>
    private readonly HashSet<int> jobbed = [];

    /// <summary>補過的 Windows 事件記錄最後一筆編號</summary>
    private long crashAfter;

    private int ticks;

    /// <summary>最後做的那件事做不到的原因；做下一件事之前一直留著</summary>
    private string fault = string.Empty;

    /// <summary>定住的清單順序（點過一列之後）；沒定住是 null</summary>
    private List<string>? order;

    /// <summary>點過的那一支；沒點過是空字串</summary>
    private string picked = string.Empty;

    /// <summary>右邊正在看、清單上反白的那一支（點過的，或第一次落到的那一支）；工具列的動作對著它</summary>
    private string showing = string.Empty;

    private string sort = "cpu";

    private bool descending = true;

    private string keyword = string.Empty;

    private string tab = "conn";

    private double width = 320;

    /// <summary>開始監控：讀 vw_monitor、有系統管理員權限就開網路量測、背景驗簽章</summary>
    /// <param name="connection">平台資料庫的連線字串</param>
    /// <param name="words">畫面上的字</param>
    public MonitorService(string connection, IReadOnlyDictionary<string, string> words)
    {
        // 讀記下來的；Windows 事件記錄從補過的最後一筆之後接著補
        store = new MonitorStore(connection);
        this.words = words;
        rows = store.QueryMonitorItems();
        crashAfter = rows.Max(row => row.record_id) ?? 0;

        // 網路要系統管理員權限
        admin = windows.QueryAdmin();
        net = admin ? new NetStore() : null;

        // 簽章一個檔要幾十到幾百毫秒，在背景一個一個驗，驗完的才出現在清單上
        new Thread(() =>
        {
            foreach (var path in unsigned.GetConsumingEnumerable())
            {
                signers.TryAdd(path, windows.QuerySigner(path));
            }
        }) { IsBackground = true, Name = "ProcessMonitor.Signer" }.Start();
    }

    /// <summary>每秒量一次：程序、網路、連線；記下新的事、套用限制；交回監控台</summary>
    public MonitorLogic.BoardView LoadTick()
    {
        lock (gate)
        {
            var now = DateTime.Now;
            ticks++;

            // 這一刻每一個程序、這一秒的網路、每一條連線
            var processes = windows.QueryProcessItems();
            var traffic = net is { Fault.Length: 0 } ? net.QueryTraffic() : default;
            links = net is { Fault.Length: 0 } ? traffic.ByLink : null;
            sockets = windows.QuerySocketItems();

            // 新的路徑讀名稱與版本、排進簽章
            foreach (var path in processes.Select(one => one.Path).Where(one => one.Length > 0 && !files.ContainsKey(one)).Distinct().ToList())
            {
                files[path] = windows.QueryFile(path);
                unsigned.Add(path);
            }

            // 併成程式、算速率
            var previous = last;
            var elapsed = previous is null ? 1 : Math.Max(0.001, (now - previous.At).TotalSeconds);
            var sample = MonitorLogic.Sampled(processes, previous?.Before ?? [], elapsed, Environment.ProcessorCount,
                net is { Fault.Length: 0 } ? traffic.ByPid : null, files, previous?.HungSince ?? [], now);
            last = sample;

            // 走勢：每一支加一點，留近 10 分鐘；不在了的丟掉
            foreach (var p in sample.Programs)
            {
                var trace = traces.TryGetValue(p.Key, out var held) ? held : traces[p.Key] = new() { ["cpu"] = [], ["mem"] = [], ["down"] = [], ["up"] = [] };
                foreach (var (key, value) in new[] { ("cpu", p.Cpu), ("mem", (double)p.Mem), ("down", p.Down ?? 0), ("up", p.Up ?? 0) })
                {
                    trace[key].Add(value);
                    trace[key].RemoveRange(0, Math.Max(0, trace[key].Count - TraceLength));
                }
            }

            traces.Keys.Except(sample.Programs.Select(p => p.Key)).ToList().ForEach(key => traces.Remove(key));

            // 聽著的埠：開始監控時就在的記成之前就在；之後才出現的記下時刻與一筆事件
            var owners = sample.Programs.SelectMany(p => p.Pids.Select(pid => (pid, p))).ToDictionary(one => one.pid, one => one.p);
            var listening = sockets.Where(one => one.Listening && owners.ContainsKey(one.Pid)).Select(one => (Program: owners[one.Pid], one.Protocol, one.Local)).DistinctBy(one => (one.Program.Key, one.Protocol, one.Local)).ToList();
            var opened = listening.Where(one => !listenSince.ContainsKey((one.Program.Key, one.Protocol, one.Local))).ToList();
            opened.ForEach(one => listenSince[(one.Program.Key, one.Protocol, one.Local)] = ticks == 1 ? null : now);

            // 開機自動執行一分鐘掃一次；Windows 事件記錄五秒補一次
            startups = ticks % 60 == 1 ? windows.QueryStartupItems() : startups;
            var crashes = ticks % 5 == 1 ? windows.QueryCrashItems(crashAfter) : [];
            crashAfter = Math.Max(crashAfter, crashes.Select(one => one.RecordId).DefaultIfEmpty(0).Max());

            // 寫下新的事：見過的程式與開機自動執行、恢復回應、開始聽埠、閃退
            var written = MonitorLogic.Seen(sample.Programs, ticks % 60 == 1 ? startups : [], rows, now)
                .Concat(MonitorLogic.Happened(sample, previous, ticks == 1 ? [] : [.. opened.Where(one => one.Protocol == "TCP").Select(one => (one.Program, one.Protocol, one.Local))], crashes, rows))
                .ToList();
            if (written.Count > 0)
            {
                store.InsertMonitorList(written);
                rows = store.QueryMonitorItems();
            }

            // CPU、記憶體上限：設了上限的程式，新開的程序放進它的 Job Object（HelixStudio 重開後第一次全部重放）
            foreach (var limit in rows.Where(row => row.type == "limit" && (row.cpu_pct is not null || row.mem_mb is not null)))
            {
                var fresh = sample.Programs.Where(p => p.Path.Equals(limit.exe_path, StringComparison.OrdinalIgnoreCase)).SelectMany(p => p.Pids).Where(pid => !jobbed.Contains(pid)).ToList();
                if (fresh.Count > 0 && admin)
                {
                    windows.SetJobCap(limit.exe_path!, fresh, limit.cpu_pct ?? 0, limit.mem_mb ?? 0);
                    jobbed.UnionWith(fresh);
                }
            }

            // 結束了的程式不再算暫停中；看著模組分頁就重讀
            paused.IntersectWith(sample.Programs.Select(p => p.Key));
            var chosen = sample.Programs.FirstOrDefault(p => p.Key == showing);
            modules = tab == "module" && chosen is not null ? windows.QueryModuleItems(chosen.Pids[0]) : modules;

            return Board(null);
        }
    }

    /// <summary>點清單的一列：換選中的那一支</summary>
    /// <param name="key">那一支的 Key</param>
    public MonitorLogic.BoardView LoadPicked(string key)
    {
        lock (gate)
        {
            picked = key;

            return Board(null);
        }
    }

    /// <summary>點欄名：同一欄再點反過來排；換一欄時數字由大到小、字由小到大</summary>
    /// <param name="key">那一欄</param>
    public MonitorLogic.BoardView LoadSorted(string key)
    {
        lock (gate)
        {
            // 照這一欄重排一次（定住的順序放掉，排好再定住）
            descending = key == sort ? !descending : key is not ("name" or "signer" or "rule");
            sort = key;
            order = null;

            return Board(null);
        }
    }

    /// <summary>搜尋框打字：名稱、路徑、簽章者有這些字的</summary>
    /// <param name="text">打的字</param>
    public MonitorLogic.BoardView LoadSearched(string text)
    {
        lock (gate)
        {
            keyword = text.Trim();

            return Board(null);
        }
    }

    /// <summary>點右邊的分頁：看載入的模組時重讀一次</summary>
    /// <param name="key">conn、port、event、module</param>
    public MonitorLogic.BoardView LoadTab(string key)
    {
        lock (gate)
        {
            tab = key;
            var chosen = last?.Programs.FirstOrDefault(p => p.Key == showing);
            modules = key == "module" && chosen is not null ? windows.QueryModuleItems(chosen.Pids[0]) : modules;

            return Board(null);
        }
    }

    /// <summary>走勢那一塊的寬變了：照新的寬重畫</summary>
    /// <param name="chartWidth">畫面上量到的寬</param>
    public MonitorLogic.BoardView LoadSized(double chartWidth)
    {
        lock (gate)
        {
            width = Math.Max(1, chartWidth);

            return Board(null);
        }
    }

    /// <summary>欄位選單勾一項：存成排成欄的那一份</summary>
    /// <param name="key">那一項</param>
    /// <param name="on">勾上或拿掉</param>
    public MonitorLogic.BoardView LoadColumn(string key, bool on)
    {
        lock (gate)
        {
            store.InsertMonitorList([MonitorLogic.Columned(rows, key, on, DateTime.Now)]);
            rows = store.QueryMonitorItems();

            return Board(null);
        }
    }

    /// <summary>改選中那一支的優先順序：每一個程序都改，記一筆</summary>
    /// <param name="priority">Windows 的優先順序類別值</param>
    public MonitorLogic.BoardView LoadPriority(int priority)
    {
        lock (gate)
        {
            // 對著選中的那一支
            var program = last?.Programs.FirstOrDefault(p => p.Key == showing);
            if (program is null || program.Priority == priority)
            {
                return Board(string.Empty);
            }

            // 叫 Windows 改；改得了才記
            var fault = windows.SetPriority(program.Pids, priority);
            if (fault.Length == 0)
            {
                store.InsertMonitorList([MonitorLogic.Acted("priority", program, program.Priority, priority, null, DateTime.Now)]);
                rows = store.QueryMonitorItems();
                program.Priority = priority;
            }

            return Board(fault);
        }
    }

    /// <summary>暫停或繼續選中那一支：暫停中就繼續，不然暫停；記一筆</summary>
    public MonitorLogic.BoardView LoadPaused()
    {
        lock (gate)
        {
            var program = last?.Programs.FirstOrDefault(p => p.Key == showing);
            if (program is null)
            {
                return Board(string.Empty);
            }

            // 叫 Windows 暫停或繼續每一條執行緒；做得了才記
            var resume = paused.Contains(program.Key);
            var fault = windows.SetSuspended(program.Pids, !resume);
            if (fault.Length == 0)
            {
                _ = resume ? paused.Remove(program.Key) : paused.Add(program.Key);
                store.InsertMonitorList([MonitorLogic.Acted(resume ? "resume" : "suspend", program, null, null, null, DateTime.Now)]);
                rows = store.QueryMonitorItems();
            }

            return Board(fault);
        }
    }

    /// <summary>按住 1 秒結束一支：每一個程序都結束，記一筆</summary>
    /// <param name="key">那一支的 Key（工具列是選中的，橫幅與小視窗是沒有回應的那一支）</param>
    public MonitorLogic.BoardView LoadEnded(string key)
    {
        lock (gate)
        {
            var program = last?.Programs.FirstOrDefault(p => p.Key == key);
            if (program is null)
            {
                return Board(string.Empty);
            }

            // 叫 Windows 結束；做得了才記
            var fault = windows.SetEnded(program.Pids);
            if (fault.Length == 0)
            {
                paused.Remove(program.Key);
                store.InsertMonitorList([MonitorLogic.Acted("end", program, null, null, null, DateTime.Now)]);
                rows = store.QueryMonitorItems();
            }

            return Board(fault);
        }
    }

    /// <summary>緩和沒有回應的那一支：它的優先順序調高、暫停同一時間 CPU 吃最多的那一支；已經在緩和就還原；記一筆</summary>
    /// <param name="key">沒有回應的那一支的 Key</param>
    public MonitorLogic.BoardView LoadEased(string key)
    {
        lock (gate)
        {
            var program = last?.Programs.FirstOrDefault(p => p.Key == key);
            var view = Board(null);
            var hang = view.Hangs.FirstOrDefault(one => one.Key == key);
            var other = last?.Programs.FirstOrDefault(p => p.Key == (eased.TryGetValue(key, out var held) ? held.Other : hang?.HogKey));
            if (program is null || other is null)
            {
                return view;
            }

            // 還原：優先順序回到原本的、被暫停的那一支繼續
            if (eased.TryGetValue(key, out var before))
            {
                var undone = string.Join("；", new[] { windows.SetPriority(program.Pids, before.Before), windows.SetSuspended(other.Pids, false) }.Where(one => one.Length > 0));
                eased.Remove(key);
                paused.Remove(other.Key);
                store.InsertMonitorList([MonitorLogic.Acted("unease", program, program.Priority, before.Before, other.Path, DateTime.Now)]);
                rows = store.QueryMonitorItems();
                program.Priority = before.Before;

                return Board(undone);
            }

            // 緩和：它調成「高」、暫停吃最多的那一支
            var fault = string.Join("；", new[] { windows.SetPriority(program.Pids, 128), windows.SetSuspended(other.Pids, true) }.Where(one => one.Length > 0));
            eased[key] = (other.Key, program.Priority);
            paused.Add(other.Key);
            store.InsertMonitorList([MonitorLogic.Acted("ease", program, program.Priority, 128, other.Path, DateTime.Now)]);
            rows = store.QueryMonitorItems();
            program.Priority = 128;

            return Board(fault);
        }
    }

    /// <summary>打開限制對話框：選中那一支現在設的上限、現在與近 10 分鐘最高的值</summary>
    public MonitorLogic.LimitView? LoadLimit()
    {
        lock (gate)
        {
            var program = last?.Programs.FirstOrDefault(p => p.Key == showing);

            return program is null ? null : MonitorLogic.Limit(program, rows, Traces(program.Key), sockets, admin && net is { Fault.Length: 0 });
        }
    }

    /// <summary>限制對話框按套用：檢查填的值，只叫 Windows 做改了的項目（上傳上限、防火牆、Job Object），做到的記一筆；回傳看不懂或做不到的原因（都做到是空字串）</summary>
    /// <param name="key">那一支的 Key</param>
    /// <param name="up">上傳上限 MB/s 的字</param>
    /// <param name="block">不准連網路</param>
    /// <param name="cpu">CPU 上限 % 的字</param>
    /// <param name="mem">記憶體上限 MB 的字</param>
    public string LoadLimited(string key, string up, bool block, string cpu, string mem)
    {
        lock (gate)
        {
            // 檢查填的值
            var program = last?.Programs.FirstOrDefault(p => p.Key == key);
            if (program is null)
            {
                return string.Empty;
            }

            var (fault, row) = MonitorLogic.Limited(program, up, block, cpu, mem, words, DateTime.Now);
            if (row is null)
            {
                return fault;
            }

            // 只叫 Windows 做這一次改了的項目：上傳上限（QoS）、不准連網路（防火牆）、CPU 與記憶體上限（Job Object）
            var before = rows.FirstOrDefault(one => one.type == "limit" && $"{one.exe_path}".Equals(program.Path, StringComparison.OrdinalIgnoreCase));
            var upFault = row.up_kbps != before?.up_kbps ? windows.SetUploadCap(program.Path, row.up_kbps ?? 0) : string.Empty;
            var blockFault = (row.block == true) != (before?.block == true) ? windows.SetBlocked(program.Path, row.block == true) : string.Empty;
            var jobChanged = row.cpu_pct != before?.cpu_pct || row.mem_mb != before?.mem_mb;
            var jobFault = jobChanged ? windows.SetJobCap(program.Path, program.Pids, row.cpu_pct ?? 0, row.mem_mb ?? 0) : string.Empty;
            if (jobChanged && jobFault.Length == 0)
            {
                jobbed.UnionWith(program.Pids);
            }

            // 照實記：做到的照新的值，做不到的保持原本的值
            var (faults, applied) = MonitorLogic.Applied(row, before, upFault, blockFault, jobFault, admin, words);
            if (applied is not null)
            {
                store.InsertMonitorList([applied]);
                rows = store.QueryMonitorItems();
            }

            return faults;
        }
    }

    /// <summary>以系統管理員重新開啟 HelixStudio；回傳做不到的原因</summary>
    public string LoadElevated() =>
        windows.SetElevated();

    /// <summary>工作列上 HelixStudio 那一格一直閃，直到被叫到前面</summary>
    /// <param name="window">主視窗</param>
    public void LoadFlashed(IntPtr window) =>
        windows.SetFlashing(window);

    /// <summary>停掉網路量測與簽章執行緒</summary>
    public void Dispose()
    {
        unsigned.CompleteAdding();
        net?.Dispose();
        windows.Dispose();
    }

    /// <summary>用最後一次量到的值重攤監控台（點選、排序、搜尋、做完一件事）</summary>
    /// <param name="done">剛才做的那件事做不到的原因；不是做事（每秒量、點選、排序）是 null，留著上一件的</param>
    private MonitorLogic.BoardView Board(string? done)
    {
        fault = done ?? fault;
        if (last is null)
        {
            return new MonitorLogic.BoardView { Admin = admin, Fault = fault };
        }

        // 點過的那一支還在就看它；沒點過就一直看第一次落到的那一支（工具列的動作對著它，不跟著清單排序換掉）；它結束了才換清單最上面那一支
        MonitorLogic.BoardView Draw(string key) =>
            MonitorLogic.Board(last, rows, sockets, links, Traces(key), signers, startups, modules, listenSince, paused,
                eased.ToDictionary(one => one.Key, one => one.Value.Other), self, admin, net?.Fault ?? string.Empty, words, key, sort, descending, keyword, order, tab, width);

        var view = Draw(last.Programs.Any(p => p.Key == picked) ? picked : showing);

        // 換了一支：看著模組分頁就讀它的模組（第一秒還沒有速率，排在最上面的不算數，第二秒才定下來）
        if (view.Picked != showing && (ticks > 1 || picked.Length > 0))
        {
            showing = view.Picked;
            var chosen = last.Programs.FirstOrDefault(p => p.Key == showing);
            modules = tab == "module" && chosen is not null ? windows.QueryModuleItems(chosen.Pids[0]) : [];
            view = Draw(showing);
        }

        // 點過一列之後清單順序定住（像工作管理員），數值照樣每秒換；沒點過就每秒照欄重排
        // （搜尋濾掉的那幾支保留原位，清掉搜尋時回到原來的地方；結束了的拿掉）
        var alive = last.Programs.Select(p => p.Key).ToHashSet();
        order = alive.Contains(picked) ? [.. (order ?? []).Concat(view.Rows.Select(row => row.Key)).Distinct().Where(alive.Contains)] : null;

        view.Fault = fault;
        return view;
    }

    /// <summary>一支程式的走勢</summary>
    private Dictionary<string, double[]> Traces(string key) =>
        traces.TryGetValue(key, out var trace) ? trace.ToDictionary(one => one.Key, one => one.Value.ToArray()) : [];
}
