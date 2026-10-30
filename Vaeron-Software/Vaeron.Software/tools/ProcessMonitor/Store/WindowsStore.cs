using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace ProcessMonitor.Store;

/// <summary>這個主題唯一問 Windows、叫 Windows 做事的型別</summary>
/// <remarks>
/// 讀：每一個程序的值、沒有回應的視窗、GPU、連線與聽著的埠、簽章、檔案描述、開機自動執行、閃退與無回應的 Windows 事件記錄、載入的模組。
/// 做：優先順序、暫停、繼續、結束、不准連網路（防火牆）、上傳上限（QoS）、CPU 與記憶體上限（Job Object）、以系統管理員重新開啟。
/// 做不到就照 Windows 回的原因照實回，不吞掉。
/// </remarks>
internal sealed class WindowsStore : IDisposable
{
    /// <summary>GPU 用的 PDH 查詢：開一次，每秒收一次</summary>
    private readonly IntPtr gpuQuery;

    private readonly IntPtr gpuCounter;

    /// <summary>打開 PDH 的 GPU 引擎計數器</summary>
    public WindowsStore()
    {
        Native.PdhOpenQueryW(null, IntPtr.Zero, out gpuQuery);
        Native.PdhAddEnglishCounterW(gpuQuery, @"\GPU Engine(*)\Utilization Percentage", IntPtr.Zero, out gpuCounter);
        Native.PdhCollectQueryData(gpuQuery);
    }

    #region 專用查詢方法

    /// <summary>這一份 HelixStudio 有沒有系統管理員權限</summary>
    public bool QueryAdmin() =>
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    /// <summary>這一刻每一個程序：路徑、累計的 CPU 時間與讀寫、記憶體、優先順序、工作階段、是不是系統關鍵、執行緒、控制代碼、開始的時刻、有沒有不回應的視窗、GPU</summary>
    public List<ProcessItem> QueryProcessItems()
    {
        // 看得到的頂層視窗裡，Windows 判定沒有回應的那幾個程序（5 秒沒處理訊息）；Ghost 是 dwm 替卡住的視窗畫的替身，不是 dwm 卡住
        var hung = new HashSet<int>();
        Native.EnumWindows((window, _) =>
        {
            var kind = new StringBuilder(16);
            if (Native.IsWindowVisible(window) && Native.GetWindow(window, 4) == IntPtr.Zero && Native.IsHungAppWindow(window) && Native.GetClassNameW(window, kind, kind.Capacity) > 0 && kind.ToString() != "Ghost")
            {
                Native.GetWindowThreadProcessId(window, out var pid);
                hung.Add(pid);
            }

            return true;
        }, IntPtr.Zero);

        // GPU：每一個引擎一個值，同一個程序同一類引擎加總、取最大的那一類（和工作管理員一樣）
        var gpu = new Dictionary<int, Dictionary<string, double>>();
        Native.PdhCollectQueryData(gpuQuery);
        var size = 0u;
        Native.PdhGetFormattedCounterArrayW(gpuCounter, 0x200 | 0x8000, ref size, out _, IntPtr.Zero);
        var items = Marshal.AllocHGlobal((int)Math.Max(size, 1));
        try
        {
            if (size > 0 && Native.PdhGetFormattedCounterArrayW(gpuCounter, 0x200 | 0x8000, ref size, out var count, items) == 0)
            {
                // PDH_FMT_COUNTERVALUE_ITEM_W：名稱指標、狀態、double；x64、x86 都是 24 個位元組，值在 16
                for (var i = 0; i < count; i++)
                {
                    var item = items + (i * 24);
                    var parts = (Marshal.PtrToStringUni(Marshal.ReadIntPtr(item)) ?? string.Empty).Split('_');
                    var pidAt = Array.IndexOf(parts, "pid");
                    var typeAt = Array.IndexOf(parts, "engtype");
                    if (pidAt >= 0 && typeAt >= 0 && int.TryParse(parts[pidAt + 1], out var pid))
                    {
                        var type = string.Join("_", parts.Skip(typeAt + 1));
                        var one = gpu.TryGetValue(pid, out var found) ? found : gpu[pid] = [];
                        one[type] = one.GetValueOrDefault(type) + BitConverter.Int64BitsToDouble(Marshal.ReadInt64(item, 16));
                    }
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(items);
        }

        // 每一個程序（pid 0 是閒置，不是程式）
        var all = new List<ProcessItem>();
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                if (p.Id == 0)
                {
                    continue;
                }

                var one = new ProcessItem { Pid = p.Id, Name = p.ProcessName, Hung = hung.Contains(p.Id), Gpu = gpu.TryGetValue(p.Id, out var engines) ? engines.Values.Max() : 0 };
                var handle = Native.OpenProcess(Native.QueryLimited, false, p.Id);
                if (handle != IntPtr.Zero)
                {
                    try
                    {
                        var length = 1024u;
                        var path = new StringBuilder((int)length);
                        one.Path = Native.QueryFullProcessImageNameW(handle, 0, path, ref length) ? path.ToString() : string.Empty;
                        if (Native.GetProcessTimes(handle, out var create, out _, out var kernel, out var user))
                        {
                            one.Cpu = kernel + user;
                            one.Start = create > 0 ? DateTime.FromFileTime(create) : null;
                        }

                        if (Native.GetProcessIoCounters(handle, out var io))
                        {
                            one.Io = io.ReadBytes + io.WriteBytes;
                        }

                        if (Native.GetProcessMemoryInfo(handle, out var memory, (uint)Marshal.SizeOf<Native.MemoryCounters>()))
                        {
                            one.Mem = (long)memory.PrivateUsage;
                        }

                        one.Priority = (int)Native.GetPriorityClass(handle);
                        Native.IsProcessCritical(handle, out var critical);
                        one.Critical = critical;
                    }
                    finally
                    {
                        Native.CloseHandle(handle);
                    }
                }

                Native.ProcessIdToSessionId(p.Id, out var session);
                one.Session = (int)session;
                try
                {
                    one.Threads = p.Threads.Count;
                    one.Handles = p.HandleCount;
                }
                catch (InvalidOperationException)
                {
                    // 量到一半它結束了
                }

                all.Add(one);
            }
        }

        return all;
    }

    /// <summary>這台電腦上全部的 TCP、UDP（IPv4、IPv6），帶擁有的程序</summary>
    public List<SocketItem> QuerySocketItems()
    {
        var all = new List<SocketItem>();
        foreach (var (family, tcp) in new[] { (2, true), (23, true), (2, false), (23, false) })
        {
            // 先問要多大、再拿；表在兩次之間變大就再問一次
            var size = 0;
            _ = tcp ? Native.GetExtendedTcpTable(IntPtr.Zero, ref size, false, family, 5, 0) : Native.GetExtendedUdpTable(IntPtr.Zero, ref size, false, family, 1, 0);
            for (var tries = 0; tries < 4; tries++)
            {
                size += 4096;
                var table = Marshal.AllocHGlobal(size);
                try
                {
                    var asked = tcp ? Native.GetExtendedTcpTable(table, ref size, false, family, 5, 0) : Native.GetExtendedUdpTable(table, ref size, false, family, 1, 0);
                    if (asked != 0)
                    {
                        continue;
                    }

                    // 一列多寬、程序、位址與埠在哪：TCP_TABLE_OWNER_PID_ALL、UDP_TABLE_OWNER_PID；埠是網路位元組順序，在 DWORD 的前兩個位元組
                    var width = (tcp, family) switch { (true, 2) => 24, (true, _) => 56, (false, 2) => 12, _ => 28 };
                    for (var i = 0; i < Marshal.ReadInt32(table); i++)
                    {
                        var row = table + 4 + (i * width);
                        string At(int addressAt, int portAt) =>
                            family == 2
                                ? $"{new IPAddress((uint)Marshal.ReadInt32(row, addressAt))}:{(Marshal.ReadByte(row, portAt) << 8) | Marshal.ReadByte(row, portAt + 1)}"
                                : $"[{new IPAddress(Enumerable.Range(0, 16).Select(b => Marshal.ReadByte(row, addressAt + b)).ToArray())}]:{(Marshal.ReadByte(row, portAt) << 8) | Marshal.ReadByte(row, portAt + 1)}";

                        all.Add((tcp, family) switch
                        {
                            (true, 2) => new SocketItem { Pid = Marshal.ReadInt32(row, 20), Protocol = "TCP", Local = At(4, 8), Remote = At(12, 16), Listening = Marshal.ReadInt32(row) == 2 },
                            (true, _) => new SocketItem { Pid = Marshal.ReadInt32(row, 52), Protocol = "TCP", Local = At(0, 20), Remote = At(24, 44), Listening = Marshal.ReadInt32(row, 48) == 2 },
                            (false, 2) => new SocketItem { Pid = Marshal.ReadInt32(row, 8), Protocol = "UDP", Local = At(0, 4), Remote = string.Empty, Listening = true },
                            _ => new SocketItem { Pid = Marshal.ReadInt32(row, 24), Protocol = "UDP", Local = At(0, 20), Remote = string.Empty, Listening = true },
                        });
                    }

                    break;
                }
                finally
                {
                    Marshal.FreeHGlobal(table);
                }
            }
        }

        return all;
    }

    /// <summary>一個檔案的名稱（檔案描述，沒有就是檔名）與版本</summary>
    /// <param name="path">檔案路徑</param>
    public (string Name, string Version) QueryFile(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);

            return (string.IsNullOrWhiteSpace(info.FileDescription) ? Path.GetFileNameWithoutExtension(path) : info.FileDescription.Trim(), $"{info.ProductVersion ?? info.FileVersion}".Trim());
        }
        catch (FileNotFoundException)
        {
            return (Path.GetFileNameWithoutExtension(path), string.Empty);
        }
    }

    /// <summary>一個檔案的簽章：簽章者的名稱；沒有簽章是空字串；有但驗不過是「簽章無效：原因」</summary>
    /// <remarks>先看檔案內嵌的簽章，沒有再找系統目錄簽章（Windows 自己的檔大多簽在目錄裡）。WinVerifyTrust 不查撤銷、不跳任何視窗。</remarks>
    /// <param name="path">檔案路徑</param>
    public string QuerySigner(string path)
    {
        var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        var p = IntPtr.Size;
        var file = Marshal.StringToHGlobalUni(path);
        var fileInfo = Marshal.AllocHGlobal(p * 4);
        var data = Marshal.AllocHGlobal(p == 8 ? 88 : 52);
        var admin = IntPtr.Zero;
        var catalog = IntPtr.Zero;
        var stream = (FileStream?)null;
        var hold = new List<IntPtr>();
        try
        {
            // WINTRUST_FILE_INFO：cbStruct、路徑、檔案代碼、已知主體
            Marshal.WriteInt32(fileInfo, p * 4);
            Marshal.WriteIntPtr(fileInfo, p, file);
            Marshal.WriteIntPtr(fileInfo, p * 2, IntPtr.Zero);
            Marshal.WriteIntPtr(fileInfo, p * 3, IntPtr.Zero);

            // 驗兩次（內嵌、系統目錄）：WINTRUST_DATA 照 x64／x86 的對齊寫，驗過就從簽章鏈拿簽章者
            (int Code, string Name) Verify(uint choice, IntPtr info)
            {
                var size = p == 8 ? 88 : 52;
                for (var i = 0; i < size; i++)
                {
                    Marshal.WriteByte(data, i, 0);
                }

                var at = 0;
                void Int(int value) { Marshal.WriteInt32(data, at, value); at += 4; }
                void Ptr(IntPtr value) { at = (at + p - 1) / p * p; Marshal.WriteIntPtr(data, at, value); at += p; }
                Int(size);
                Ptr(IntPtr.Zero);
                Ptr(IntPtr.Zero);
                Int(2);
                Int(0);
                Int((int)choice);
                Ptr(info);
                var actionAt = at;
                Int(1);
                var stateAt = (at + p - 1) / p * p;
                Ptr(IntPtr.Zero);
                Ptr(IntPtr.Zero);
                Int(0x10 | 0x1000);

                var code = Native.WinVerifyTrust(new IntPtr(-1), ref action, data);
                var name = string.Empty;
                if (code == 0)
                {
                    var signer = Native.WTHelperGetProvSignerFromChain(Native.WTHelperProvDataFromStateData(Marshal.ReadIntPtr(data, stateAt)), 0, false, 0);
                    var chain = signer == IntPtr.Zero ? IntPtr.Zero : Marshal.ReadIntPtr(signer, 16);
                    var cert = chain == IntPtr.Zero ? IntPtr.Zero : Marshal.ReadIntPtr(chain, p);
                    name = cert == IntPtr.Zero ? string.Empty : new X509Certificate2(cert).GetNameInfo(X509NameType.SimpleName, false);
                }

                // 關掉這一次驗證留下的狀態
                Marshal.WriteInt32(data, actionAt, 2);
                Native.WinVerifyTrust(new IntPtr(-1), ref action, data);
                return (code, name);
            }

            // 檔案內嵌的簽章
            var embedded = Verify(1, fileInfo);
            if (embedded.Code == 0)
            {
                return embedded.Name;
            }

            // 有簽章但驗不過（0x800B0100 是沒有簽章）
            if (embedded.Code != unchecked((int)0x800B0100))
            {
                return $"簽章無效：{new System.ComponentModel.Win32Exception(embedded.Code).Message}";
            }

            // 系統目錄：算這個檔的雜湊，找收著它的目錄檔
            stream = File.OpenRead(path);
            var handle = stream.SafeFileHandle.DangerousGetHandle();
            if (!Native.CryptCATAdminAcquireContext2(out admin, IntPtr.Zero, "SHA256", IntPtr.Zero, 0))
            {
                return string.Empty;
            }

            var hashSize = 0u;
            Native.CryptCATAdminCalcHashFromFileHandle2(admin, handle, ref hashSize, null, 0);
            var hash = new byte[hashSize];
            if (hashSize == 0 || !Native.CryptCATAdminCalcHashFromFileHandle2(admin, handle, ref hashSize, hash, 0))
            {
                return string.Empty;
            }

            catalog = Native.CryptCATAdminEnumCatalogFromHash(admin, hash, hashSize, 0, IntPtr.Zero);
            if (catalog == IntPtr.Zero)
            {
                return string.Empty;
            }

            // CATALOG_INFO：cbStruct ＋ 260 個字的目錄檔路徑
            var catInfo = Marshal.AllocHGlobal(4 + (260 * 2));
            hold.Add(catInfo);
            Marshal.WriteInt32(catInfo, 4 + (260 * 2));
            Native.CryptCATCatalogInfoFromContext(catalog, catInfo, 0);
            var catPath = Marshal.StringToHGlobalUni(Marshal.PtrToStringUni(catInfo + 4));
            var memberTag = Marshal.StringToHGlobalUni(Convert.ToHexString(hash));
            var hashCopy = Marshal.AllocHGlobal(hash.Length);
            hold.AddRange([catPath, memberTag, hashCopy]);
            Marshal.Copy(hash, 0, hashCopy, hash.Length);

            // WINTRUST_CATALOG_INFO
            var info = Marshal.AllocHGlobal(8 + (p * 6) + 8 + (p * 2));
            hold.Add(info);
            var at = 0;
            void CatInt(int value) { Marshal.WriteInt32(info, at, value); at += 4; }
            void CatPtr(IntPtr value) { at = (at + p - 1) / p * p; Marshal.WriteIntPtr(info, at, value); at += p; }
            CatInt(0);
            CatInt(0);
            CatPtr(catPath);
            CatPtr(memberTag);
            CatPtr(file);
            CatPtr(handle);
            CatPtr(hashCopy);
            CatInt(hash.Length);
            CatPtr(IntPtr.Zero);
            CatPtr(admin);
            Marshal.WriteInt32(info, (at + p - 1) / p * p);

            var listed = Verify(2, info);
            return listed.Code == 0 ? listed.Name : $"簽章無效：{new System.ComponentModel.Win32Exception(listed.Code).Message}";
        }
        finally
        {
            hold.ForEach(Marshal.FreeHGlobal);
            if (catalog != IntPtr.Zero)
            {
                Native.CryptCATAdminReleaseCatalogContext(admin, catalog, 0);
            }

            if (admin != IntPtr.Zero)
            {
                Native.CryptCATAdminReleaseContext(admin, 0);
            }

            stream?.Dispose();
            Marshal.FreeHGlobal(data);
            Marshal.FreeHGlobal(fileInfo);
            Marshal.FreeHGlobal(file);
        }
    }

    /// <summary>一個程序載入的模組：檔名與版本；讀不到就是一列原因</summary>
    /// <param name="pid">那一個程序</param>
    public List<(string Name, string Version)> QueryModuleItems(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);

            return [.. p.Modules.OfType<ProcessModule>().Select(one => (one.ModuleName, $"{one.FileVersionInfo.FileVersion}".Trim()))];
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
        {
            return [($"讀不到：{error.Message}", string.Empty)];
        }
    }

    /// <summary>這台電腦的開機自動執行：登錄檔 Run／RunOnce、啟動資料夾、開機或登入觸發的排程工作、自動啟動的服務</summary>
    public List<StartupItem> QueryStartupItems()
    {
        var found = new List<StartupItem>();
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        // 一行命令列裡的程式路徑：引號裡的、或到 .exe 為止；環境變數與 \SystemRoot\、\??\、system32\ 展開
        string Command(string line)
        {
            line = Environment.ExpandEnvironmentVariables(line.Trim());
            var path = line.StartsWith('"')
                ? line[1..].Split('"')[0]
                : line.IndexOf(".exe", StringComparison.OrdinalIgnoreCase) is var end and >= 0 ? line[..(end + 4)] : line.Split(' ')[0];
            path = path.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase) ? Path.Combine(windows, path[12..]) : path;
            path = path.StartsWith(@"\??\") ? path[4..] : path;
            return path.StartsWith(@"system32\", StringComparison.OrdinalIgnoreCase) ? Path.Combine(windows, path) : path;
        }

        // 登錄檔 Run、RunOnce（含 32 位元那一份）
        foreach (var (root, rootName) in new[] { (Registry.LocalMachine, "HKLM"), (Registry.CurrentUser, "HKCU") })
        {
            foreach (var (sub, source) in new[] { (@"Software\Microsoft\Windows\CurrentVersion\Run", "run"), (@"Software\Microsoft\Windows\CurrentVersion\RunOnce", "runonce"), (@"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "run") })
            {
                using var key = root.OpenSubKey(sub);
                foreach (var name in key?.GetValueNames() ?? [])
                {
                    found.Add(new StartupItem { Path = Command($"{key!.GetValue(name)}"), Source = source, Location = $@"{rootName}\{sub}\{name}" });
                }
            }
        }

        // 啟動資料夾：捷徑照它指到的檔案
        var shell = Type.GetTypeFromProgID("WScript.Shell") is { } shellType ? Activator.CreateInstance(shellType) : null;
        foreach (var folder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Startup), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup) })
        {
            foreach (var file in Directory.Exists(folder) ? Directory.GetFiles(folder) : [])
            {
                var target = file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && shell is not null
                    ? $"{((dynamic)shell).CreateShortcut(file).TargetPath}"
                    : file;
                found.Add(new StartupItem { Path = target, Source = "folder", Location = file });
            }
        }

        // 排程工作：有開機（8）或登入（9）觸發的，執行（0）那一類動作的程式
        try
        {
            dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!;
            service.Connect();
            var folders = new Stack<dynamic>();
            folders.Push(service.GetFolder("\\"));
            while (folders.Count > 0)
            {
                var folder = folders.Pop();
                foreach (var sub in folder.GetFolders(0))
                {
                    folders.Push(sub);
                }

                foreach (var task in folder.GetTasks(1))
                {
                    var atStart = false;
                    foreach (var trigger in task.Definition.Triggers)
                    {
                        atStart |= (int)trigger.Type is 8 or 9;
                    }

                    foreach (var act in task.Definition.Actions)
                    {
                        if (atStart && (int)act.Type == 0)
                        {
                            found.Add(new StartupItem { Path = Command($"{act.Path}"), Source = "task", Location = $"{task.Path}" });
                        }
                    }
                }
            }
        }
        catch (Exception error) when (error is COMException or UnauthorizedAccessException)
        {
            // 工作排程器讀不到：其他來源照樣列
        }

        // 自動啟動（Start = 2）的服務
        using var services = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
        foreach (var name in services?.GetSubKeyNames() ?? [])
        {
            using var service = services!.OpenSubKey(name);
            if (service?.GetValue("Start") is int start && start == 2 && service.GetValue("Type") is int type && (type & 0x30) != 0 && service.GetValue("ImagePath") is string image)
            {
                found.Add(new StartupItem { Path = Command(image), Source = "service", Location = name });
            }
        }

        return found.Where(one => one.Path.Length > 0).DistinctBy(one => (one.Path.ToLowerInvariant(), one.Source, one.Location)).ToList();
    }

    /// <summary>Windows 應用程式記錄裡的閃退（1000 Application Error、1026 .NET Runtime）與無回應被結束（1002 Application Hang）；只拿編號比 after 新的，新的在前讀到 after 為止</summary>
    /// <param name="after">已經補過的最後一筆編號；0 是全部</param>
    public List<CrashItem> QueryCrashItems(long after)
    {
        var found = new List<CrashItem>();
        var query = new EventLogQuery("Application", PathType.LogName, "*[System[(EventID=1000 or EventID=1002 or EventID=1026)]]") { ReverseDirection = true };
        using var reader = new EventLogReader(query);
        for (var record = reader.ReadEvent(); record is not null; record = reader.ReadEvent())
        {
            using (record)
            {
                if ((record.RecordId ?? 0) <= after)
                {
                    break;
                }

                var values = record.Properties.Select(one => $"{one.Value}").ToList();
                var at = record.TimeCreated ?? DateTime.Now;
                switch (record.Id, record.ProviderName)
                {
                    // 1000：程式名、版本、…、模組名、…、例外碼、…、程式路徑
                    case (1000, "Application Error") when values.Count > 10:
                        found.Add(new CrashItem { RecordId = record.RecordId ?? 0, At = at, Kind = "crash", ExePath = values[10], Name = Path.GetFileNameWithoutExtension(values[0]), Module = values[3], Code = values[6].StartsWith("0x") ? values[6] : "0x" + values[6] });
                        break;

                    // 1002：程式名、版本、程序、開始、結束、程式路徑
                    case (1002, "Application Hang") when values.Count > 5:
                        found.Add(new CrashItem { RecordId = record.RecordId ?? 0, At = at, Kind = "hang", ExePath = values[5], Name = Path.GetFileNameWithoutExtension(values[0]) });
                        break;

                    // 1026：一整段訊息，Application: 那一行是程式檔名，Exception Info: 那一行是例外
                    case (1026, ".NET Runtime") when values.Count > 0:
                        var lines = values[0].Split('\n').Select(one => one.Trim()).ToList();
                        var app = lines.Where(one => one.StartsWith("Application:")).Select(one => one["Application:".Length..].Trim()).FirstOrDefault() ?? string.Empty;
                        var exception = lines.Where(one => one.StartsWith("Exception Info:")).Select(one => one["Exception Info:".Length..].Trim()).FirstOrDefault() ?? string.Empty;
                        found.Add(new CrashItem { RecordId = record.RecordId ?? 0, At = at, Kind = "crash", ExePath = app, Name = Path.GetFileNameWithoutExtension(app), Module = exception.Length > 255 ? exception[..255] : exception });
                        break;
                }
            }
        }

        return found;
    }

    #endregion

    #region 叫 Windows 做的事

    /// <summary>改優先順序：每一個程序都改；回傳做不到的原因（全部做到是空字串）</summary>
    /// <param name="pids">那一支程式的每一個程序</param>
    /// <param name="priority">Windows 的優先順序類別值</param>
    public string SetPriority(IReadOnlyList<int> pids, int priority) =>
        string.Join("；", pids.Select(pid =>
        {
            var handle = Native.OpenProcess(Native.SetInformation | Native.QueryLimited, false, pid);
            if (handle == IntPtr.Zero)
            {
                return new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message;
            }

            var done = Native.SetPriorityClass(handle, (uint)priority);
            var why = done ? string.Empty : new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message;
            Native.CloseHandle(handle);
            return why;
        }).Where(one => one.Length > 0).Distinct());

    /// <summary>暫停（true）或繼續（false）：每一個程序的每一條執行緒；回傳做不到的原因</summary>
    /// <param name="pids">那一支程式的每一個程序</param>
    /// <param name="suspend">暫停或繼續</param>
    public string SetSuspended(IReadOnlyList<int> pids, bool suspend) =>
        string.Join("；", pids.Select(pid =>
        {
            var handle = Native.OpenProcess(Native.SuspendResume, false, pid);
            if (handle == IntPtr.Zero)
            {
                return new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message;
            }

            var status = suspend ? Native.NtSuspendProcess(handle) : Native.NtResumeProcess(handle);
            Native.CloseHandle(handle);
            return status == 0 ? string.Empty : $"NTSTATUS 0x{status:x8}";
        }).Where(one => one.Length > 0).Distinct());

    /// <summary>結束：每一個程序；回傳做不到的原因</summary>
    /// <param name="pids">那一支程式的每一個程序</param>
    public string SetEnded(IReadOnlyList<int> pids) =>
        string.Join("；", pids.Select(pid =>
        {
            var handle = Native.OpenProcess(Native.Terminate, false, pid);
            if (handle == IntPtr.Zero)
            {
                return new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message;
            }

            var done = Native.TerminateProcess(handle, 1);
            var why = done ? string.Empty : new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message;
            Native.CloseHandle(handle);
            return why;
        }).Where(one => one.Length > 0).Distinct());

    /// <summary>不准連網路：Windows 防火牆對這支程式加（或拿掉）連出、連入兩條封鎖規則；回傳做不到的原因</summary>
    /// <param name="path">那一支程式的路徑</param>
    /// <param name="block">加上（true）或拿掉（false）</param>
    public string SetBlocked(string path, bool block)
    {
        try
        {
            dynamic policy = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2")!)!;
            foreach (var (direction, word) in new[] { (2, "連出"), (1, "連入") })
            {
                var name = $"HelixStudio 程式監控 · {word} · {path}";

                // 同名的先拿掉（拿到沒有為止），要加再加一條
                for (var tries = 0; tries < 8; tries++)
                {
                    try
                    {
                        policy.Rules.Remove(name);
                    }
                    catch (COMException)
                    {
                        break;
                    }
                }

                if (block)
                {
                    dynamic rule = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule")!)!;
                    rule.Name = name;
                    rule.ApplicationName = path;
                    rule.Action = 0;
                    rule.Direction = direction;
                    rule.Enabled = true;
                    rule.Profiles = 0x7FFFFFFF;
                    policy.Rules.Add(rule);
                }
            }

            return string.Empty;
        }
        catch (Exception error) when (error is COMException or UnauthorizedAccessException)
        {
            return error.Message;
        }
    }

    /// <summary>上傳上限：本機群組原則的 QoS 原則（New-NetQosPolicy）；0 是拿掉；回傳做不到的原因</summary>
    /// <remarks>名稱、路徑、速率用環境變數交給 PowerShell，命令本身是固定的字，不把值接進命令；做不到時只交回 Windows 給的那一句原因。</remarks>
    /// <param name="path">那一支程式的路徑</param>
    /// <param name="kbps">上限 KB/s；0 是拿掉</param>
    public string SetUploadCap(string path, int kbps)
    {
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add("$ErrorActionPreference = 'Stop'; try { Get-NetQosPolicy -Name $env:HX_QOS_NAME -ErrorAction SilentlyContinue | Remove-NetQosPolicy -Confirm:$false; if ([uint64]$env:HX_QOS_BITS -gt 0) { New-NetQosPolicy -Name $env:HX_QOS_NAME -AppPathNameMatchCondition $env:HX_QOS_PATH -ThrottleRateActionBitsPerSecond ([uint64]$env:HX_QOS_BITS) | Out-Null } } catch { [Console]::Error.Write($_.Exception.Message); exit 1 }");
        start.Environment["HX_QOS_NAME"] = $"HelixStudio 程式監控 · {Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToLowerInvariant())))[..16]}";
        start.Environment["HX_QOS_PATH"] = path;
        start.Environment["HX_QOS_BITS"] = $"{(long)kbps * 1024 * 8}";

        using var shell = Process.Start(start)!;
        var error = shell.StandardError.ReadToEnd();
        shell.WaitForExit();
        return shell.ExitCode == 0 ? string.Empty : error.Trim();
    }

    /// <summary>CPU、記憶體上限：這支程式一個有名字的 Job Object（之後啟動的同一支也放進去）；0 是不限；回傳做不到的原因</summary>
    /// <param name="path">那一支程式的路徑</param>
    /// <param name="pids">那一支程式現在的每一個程序</param>
    /// <param name="cpuPct">CPU 上限 %；0 是不限</param>
    /// <param name="memMb">每一個程序的記憶體上限 MB；0 是不限</param>
    public string SetJobCap(string path, IReadOnlyList<int> pids, int cpuPct, int memMb)
    {
        var job = Native.CreateJobObjectW(IntPtr.Zero, $"HelixStudio.Monitor.{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToLowerInvariant())))[..16]}");
        if (job == IntPtr.Zero)
        {
            return new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message;
        }

        var faults = new List<string>();

        // CPU：JOBOBJECT_CPU_RATE_CONTROL_INFORMATION，開啟（1）＋硬上限（4），比例是萬分之幾
        var cpu = Marshal.AllocHGlobal(8);
        Marshal.WriteInt32(cpu, cpuPct > 0 ? 0x1 | 0x4 : 0);
        Marshal.WriteInt32(cpu, 4, cpuPct * 100);
        faults.AddRange(new[] { Native.SetInformationJobObject(job, 15, cpu, 8) }.Where(one => !one).Select(_ => new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message));
        Marshal.FreeHGlobal(cpu);

        // 記憶體：JOBOBJECT_EXTENDED_LIMIT_INFORMATION（基本限制的 LimitFlags 在 16，ProcessMemoryLimit 在基本限制與 IO_COUNTERS 之後）
        var basic = IntPtr.Size == 8 ? 64 : 48;
        var size = basic + 48 + (IntPtr.Size * 4);
        var memory = Marshal.AllocHGlobal(size);
        for (var i = 0; i < size; i++)
        {
            Marshal.WriteByte(memory, i, 0);
        }

        Marshal.WriteInt32(memory, 16, memMb > 0 ? 0x100 : 0);
        Marshal.WriteIntPtr(memory, basic + 48, new IntPtr((long)memMb * 1024 * 1024));
        faults.AddRange(new[] { Native.SetInformationJobObject(job, 9, memory, (uint)size) }.Where(one => !one).Select(_ => new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message));
        Marshal.FreeHGlobal(memory);

        // 現在的每一個程序放進去
        foreach (var pid in pids)
        {
            var handle = Native.OpenProcess(Native.SetQuota | Native.Terminate, false, pid);
            if (handle == IntPtr.Zero || !Native.AssignProcessToJobObject(job, handle))
            {
                faults.Add(new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message);
            }

            if (handle != IntPtr.Zero)
            {
                Native.CloseHandle(handle);
            }
        }

        // 留著代碼：HelixStudio 開著的期間 Job Object 一直在；關掉之後，裡面的程序還在它就還在
        return string.Join("；", faults.Distinct());
    }

    /// <summary>以系統管理員重新開啟 HelixStudio：新的那一份等這一份關掉才接手；回傳做不到的原因（操作員在 UAC 按否也是）</summary>
    public string SetElevated()
    {
        try
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" };
            start.ArgumentList.Add("--after");
            start.ArgumentList.Add($"{Environment.ProcessId}");
            Process.Start(start);
            return string.Empty;
        }
        catch (System.ComponentModel.Win32Exception error)
        {
            return error.NativeErrorCode == 1223 ? "沒有同意系統管理員權限" : error.Message;
        }
    }

    /// <summary>工作列上那一格一直閃，直到視窗被叫到前面</summary>
    /// <param name="window">那一個視窗</param>
    public void SetFlashing(IntPtr window)
    {
        var info = new Native.FlashInfo { Size = (uint)Marshal.SizeOf<Native.FlashInfo>(), Window = window, Flags = 0x3 | 0xC, Count = 0, Timeout = 0 };
        Native.FlashWindowEx(ref info);
    }

    #endregion

    /// <summary>關掉 PDH 查詢</summary>
    public void Dispose() =>
        Native.PdhCloseQuery(gpuQuery);

    #region 模型

    /// <summary>一個程序這一刻的值</summary>
    public class ProcessItem
    {
        /// <summary>程序編號</summary>
        public int Pid { get; set; }

        /// <summary>程序名稱（沒有路徑時用它認）</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>完整路徑；讀不到是空字串</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>累計的 CPU 時間（100ns）</summary>
        public long Cpu { get; set; }

        /// <summary>累計的讀寫位元組</summary>
        public ulong Io { get; set; }

        /// <summary>私人位元組</summary>
        public long Mem { get; set; }

        /// <summary>Windows 的優先順序類別值</summary>
        public int Priority { get; set; }

        /// <summary>登入工作階段</summary>
        public int Session { get; set; }

        /// <summary>系統關鍵程序（結束了 Windows 會當機）</summary>
        public bool Critical { get; set; }

        public int Threads { get; set; }

        public int Handles { get; set; }

        /// <summary>開始的時刻</summary>
        public DateTime? Start { get; set; }

        /// <summary>有看得到的頂層視窗被 Windows 判定沒有回應</summary>
        public bool Hung { get; set; }

        /// <summary>GPU %</summary>
        public double Gpu { get; set; }
    }

    /// <summary>一條 TCP 連線或一個聽著的埠</summary>
    public class SocketItem
    {
        public int Pid { get; set; }

        /// <summary>TCP 或 UDP</summary>
        public string Protocol { get; set; } = string.Empty;

        public string Local { get; set; } = string.Empty;

        /// <summary>遠端位址；聽著的、UDP 是空字串</summary>
        public string Remote { get; set; } = string.Empty;

        /// <summary>聽著（TCP LISTEN 或 UDP）</summary>
        public bool Listening { get; set; }
    }

    /// <summary>一個開機自動執行</summary>
    public class StartupItem
    {
        public string Path { get; set; } = string.Empty;

        /// <summary>run、runonce、folder、task、service</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>在那裡的哪一項</summary>
        public string Location { get; set; } = string.Empty;
    }

    /// <summary>Windows 事件記錄裡的一筆閃退或無回應</summary>
    public class CrashItem
    {
        public long RecordId { get; set; }

        public DateTime At { get; set; }

        /// <summary>crash 或 hang</summary>
        public string Kind { get; set; } = string.Empty;

        public string ExePath { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>例外碼</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>出錯的模組</summary>
        public string Module { get; set; } = string.Empty;
    }

    #endregion
}
