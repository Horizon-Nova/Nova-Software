using System.Net;
using System.Runtime.InteropServices;

namespace ProcessMonitor.Store;

/// <summary>每一支程式、每一條連線送出與收到多少位元組</summary>
/// <remarks>
/// 開一個 ETW 即時工作階段，接 Microsoft-Windows-Kernel-Network（TCP、UDP，IPv4、IPv6 的送出與收到）；要系統管理員權限。
/// 收到的事件在 ProcessTrace 那一條執行緒上累加，每秒拿走一次（拿走就歸零）。
/// </remarks>
internal sealed class NetStore : IDisposable
{
    private const string Session = "HelixStudio-ProcessMonitor";

    private readonly object gate = new();

    /// <summary>這一秒每一個程序收到、送出的位元組</summary>
    private Dictionary<int, (long Down, long Up)> byPid = [];

    /// <summary>這一秒每一條連線（程序、遠端位址）收到、送出的位元組</summary>
    private Dictionary<(int Pid, string Remote), (long Down, long Up)> byLink = [];

    /// <summary>ETW 叫回來的那一支；留著參考，不然被回收</summary>
    private readonly Native.EventRecordCallback callback;

    private IntPtr properties;

    private ulong session;

    private ulong trace;

    /// <summary>開不起來的原因；開起來是空字串</summary>
    public string Fault { get; } = string.Empty;

    /// <summary>開工作階段、開始接事件；沒有權限或開不起來就記下原因</summary>
    public NetStore()
    {
        // 一個事件：照事件編號讀出程序、大小、遠端位址，加到這一秒的累計
        callback = record =>
        {
            // EVENT_RECORD：EventHeader.EventDescriptor.Id 在 40；UserDataLength 在 86；UserData 在 88 之後對齊指標
            var id = (ushort)Marshal.ReadInt16(record, 40);
            var length = (ushort)Marshal.ReadInt16(record, 86);
            var data = Marshal.ReadIntPtr(record, 88 + IntPtr.Size);

            // 10、26 TCP 送出；11、27 TCP 收到；42、58 UDP 送出；43、59 UDP 收到（前者 IPv4、後者 IPv6）
            var v4 = id is 10 or 11 or 42 or 43;
            var v6 = id is 26 or 27 or 58 or 59;
            if ((!v4 && !v6) || data == IntPtr.Zero || length < (v4 ? 20 : 44))
            {
                return;
            }

            var pid = Marshal.ReadInt32(data);
            var size = (uint)Marshal.ReadInt32(data, 4);
            var send = id is 10 or 26 or 42 or 58;
            var address = new byte[v4 ? 4 : 16];
            Marshal.Copy(data + 8, address, 0, address.Length);
            var portAt = v4 ? 16 : 40;
            var port = (Marshal.ReadByte(data, portAt) << 8) | Marshal.ReadByte(data, portAt + 1);
            var remote = v4 ? $"{new IPAddress(address)}:{port}" : $"[{new IPAddress(address)}]:{port}";

            lock (gate)
            {
                var one = byPid.GetValueOrDefault(pid);
                byPid[pid] = send ? (one.Down, one.Up + size) : (one.Down + size, one.Up);
                var link = byLink.GetValueOrDefault((pid, remote));
                byLink[(pid, remote)] = send ? (link.Down, link.Up + size) : (link.Down + size, link.Up);
            }
        };

        // EVENT_TRACE_PROPERTIES 加上放工作階段名稱的空間：即時模式、每秒交一次
        var head = Marshal.SizeOf<Native.TraceProperties>();
        var size = head + 1024;
        properties = Marshal.AllocHGlobal(size);
        for (var i = 0; i < size; i++)
        {
            Marshal.WriteByte(properties, i, 0);
        }

        Marshal.StructureToPtr(new Native.TraceProperties
        {
            Wnode = new Native.WnodeHeader { BufferSize = (uint)size, Flags = 0x20000, ClientContext = 1 },
            LogFileMode = 0x100,
            FlushTimer = 1,
            LoggerNameOffset = (uint)head,
        }, properties, false);

        // 上一次沒關乾淨（HelixStudio 被強制結束）的同名工作階段先停掉，再用同一份設定開
        Native.ControlTraceW(0, Session, properties, 1);
        Marshal.StructureToPtr(new Native.TraceProperties
        {
            Wnode = new Native.WnodeHeader { BufferSize = (uint)size, Flags = 0x20000, ClientContext = 1 },
            LogFileMode = 0x100,
            FlushTimer = 1,
            LoggerNameOffset = (uint)head,
        }, properties, true);

        var started = Native.StartTraceW(out session, Session, properties);
        if (started != 0)
        {
            Fault = new System.ComponentModel.Win32Exception(started).Message;
            return;
        }

        // TCP、UDP 的 IPv4（0x10）與 IPv6（0x20），資訊層級
        var provider = new Guid("7DD42A49-5329-4832-8DFD-43D979153A88");
        var enabled = Native.EnableTraceEx2(session, ref provider, 1, 4, 0x30, 0, 0, IntPtr.Zero);
        if (enabled != 0)
        {
            Fault = new System.ComponentModel.Win32Exception(enabled).Message;
            return;
        }

        var logfile = new Native.EventTraceLogfile
        {
            LoggerName = Session,
            ProcessTraceMode = 0x100 | 0x10000000,
            EventRecordCallback = Marshal.GetFunctionPointerForDelegate(callback),
        };
        trace = Native.OpenTraceW(ref logfile);
        if (trace == ulong.MaxValue || trace == 0xFFFFFFFF)
        {
            Fault = new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message;
            trace = 0;
            return;
        }

        // ProcessTrace 會一直卡著收事件，放在自己的執行緒；關工作階段時它就回來
        var opened = trace;
        new Thread(() => Native.ProcessTrace([opened], 1, IntPtr.Zero, IntPtr.Zero)) { IsBackground = true, Name = Session }.Start();
    }

    /// <summary>拿走這一秒累加的：每一個程序、每一條連線（拿走就歸零）</summary>
    public (Dictionary<int, (long Down, long Up)> ByPid, Dictionary<(int Pid, string Remote), (long Down, long Up)> ByLink) QueryTraffic()
    {
        lock (gate)
        {
            var taken = (byPid, byLink);
            byPid = [];
            byLink = [];
            return taken;
        }
    }

    /// <summary>停掉工作階段（ProcessTrace 那一條執行緒跟著回來）</summary>
    public void Dispose()
    {
        if (session != 0)
        {
            Native.ControlTraceW(session, null, properties, 1);
        }

        if (trace != 0)
        {
            Native.CloseTrace(trace);
        }

        Marshal.FreeHGlobal(properties);
        properties = IntPtr.Zero;
    }
}
