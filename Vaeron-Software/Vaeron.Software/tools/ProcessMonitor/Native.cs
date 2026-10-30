using System.Runtime.InteropServices;
using System.Text;

namespace ProcessMonitor;

/// <summary>Windows 的 API 宣告</summary>
/// <remarks>只有宣告與它們要的結構；怎麼問、怎麼用寫在 WindowsStore 與 NetStore。x64、x86 共用，指標大小照 IntPtr.Size。</remarks>
internal static class Native
{
    #region 程式

    public const uint QueryLimited = 0x1000;
    public const uint SuspendResume = 0x0800;
    public const uint Terminate = 0x0001;
    public const uint SetInformation = 0x0200;
    public const uint SetQuota = 0x0100;

    [DllImport("kernel32", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool QueryFullProcessImageNameW(IntPtr process, uint flags, StringBuilder name, ref uint size);

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool GetProcessTimes(IntPtr process, out long create, out long exit, out long kernel, out long user);

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool GetProcessIoCounters(IntPtr process, out IoCounters counters);

    [DllImport("kernel32", SetLastError = true, EntryPoint = "K32GetProcessMemoryInfo")]
    public static extern bool GetProcessMemoryInfo(IntPtr process, out MemoryCounters counters, uint size);

    [DllImport("kernel32", SetLastError = true)]
    public static extern uint GetPriorityClass(IntPtr process);

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool SetPriorityClass(IntPtr process, uint priority);

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool TerminateProcess(IntPtr process, uint code);

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool IsProcessCritical(IntPtr process, out bool critical);

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool ProcessIdToSessionId(int pid, out uint session);

    [DllImport("ntdll")]
    public static extern int NtSuspendProcess(IntPtr process);

    [DllImport("ntdll")]
    public static extern int NtResumeProcess(IntPtr process);

    [StructLayout(LayoutKind.Sequential)]
    public struct IoCounters
    {
        public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MemoryCounters
    {
        public uint Size, PageFaultCount;
        public UIntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage,
            QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage, PrivateUsage;
    }

    #endregion

    #region 視窗

    public delegate bool WindowVisit(IntPtr window, IntPtr data);

    [DllImport("user32")]
    public static extern bool EnumWindows(WindowVisit visit, IntPtr data);

    [DllImport("user32")]
    public static extern uint GetWindowThreadProcessId(IntPtr window, out int pid);

    [DllImport("user32")]
    public static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32")]
    public static extern bool IsHungAppWindow(IntPtr window);

    [DllImport("user32", CharSet = CharSet.Unicode)]
    public static extern int GetClassNameW(IntPtr window, StringBuilder name, int size);

    [DllImport("user32")]
    public static extern IntPtr GetWindow(IntPtr window, uint command);

    [DllImport("user32")]
    public static extern bool FlashWindowEx(ref FlashInfo info);

    [StructLayout(LayoutKind.Sequential)]
    public struct FlashInfo
    {
        public uint Size;
        public IntPtr Window;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    #endregion

    #region 連線與埠

    [DllImport("iphlpapi", SetLastError = true)]
    public static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool order, int family, int cls, uint reserved);

    [DllImport("iphlpapi", SetLastError = true)]
    public static extern uint GetExtendedUdpTable(IntPtr table, ref int size, bool order, int family, int cls, uint reserved);

    #endregion

    #region Job Object

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateJobObjectW(IntPtr security, string name);

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool SetInformationJobObject(IntPtr job, int cls, IntPtr info, uint size);

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    #endregion

    #region 簽章

    [DllImport("wintrust", CharSet = CharSet.Unicode)]
    public static extern int WinVerifyTrust(IntPtr window, ref Guid action, IntPtr data);

    [DllImport("wintrust")]
    public static extern IntPtr WTHelperProvDataFromStateData(IntPtr state);

    [DllImport("wintrust")]
    public static extern IntPtr WTHelperGetProvSignerFromChain(IntPtr provider, uint signer, bool counter, uint counterIndex);

    [DllImport("wintrust", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CryptCATAdminAcquireContext2(out IntPtr admin, IntPtr subsystem, string algorithm, IntPtr policy, uint flags);

    [DllImport("wintrust", SetLastError = true)]
    public static extern bool CryptCATAdminCalcHashFromFileHandle2(IntPtr admin, IntPtr file, ref uint size, byte[]? hash, uint flags);

    [DllImport("wintrust", SetLastError = true)]
    public static extern IntPtr CryptCATAdminEnumCatalogFromHash(IntPtr admin, byte[] hash, uint size, uint flags, IntPtr previous);

    [DllImport("wintrust", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CryptCATCatalogInfoFromContext(IntPtr catalog, IntPtr info, uint flags);

    [DllImport("wintrust")]
    public static extern bool CryptCATAdminReleaseCatalogContext(IntPtr admin, IntPtr catalog, uint flags);

    [DllImport("wintrust")]
    public static extern bool CryptCATAdminReleaseContext(IntPtr admin, uint flags);

    #endregion

    #region GPU（PDH）

    [DllImport("pdh", CharSet = CharSet.Unicode)]
    public static extern int PdhOpenQueryW(string? source, IntPtr user, out IntPtr query);

    [DllImport("pdh", CharSet = CharSet.Unicode)]
    public static extern int PdhAddEnglishCounterW(IntPtr query, string path, IntPtr user, out IntPtr counter);

    [DllImport("pdh")]
    public static extern int PdhCollectQueryData(IntPtr query);

    [DllImport("pdh")]
    public static extern int PdhCloseQuery(IntPtr query);

    [DllImport("pdh", CharSet = CharSet.Unicode)]
    public static extern int PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint size, out uint count, IntPtr items);

    #endregion

    #region ETW

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void EventRecordCallback(IntPtr record);

    [DllImport("advapi32", CharSet = CharSet.Unicode)]
    public static extern int StartTraceW(out ulong session, string name, IntPtr properties);

    [DllImport("advapi32", CharSet = CharSet.Unicode)]
    public static extern int ControlTraceW(ulong session, string? name, IntPtr properties, uint code);

    [DllImport("advapi32")]
    public static extern int EnableTraceEx2(ulong session, ref Guid provider, uint code, byte level, ulong any, ulong all, uint timeout, IntPtr parameters);

    [DllImport("advapi32", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern ulong OpenTraceW(ref EventTraceLogfile logfile);

    [DllImport("advapi32")]
    public static extern int ProcessTrace(ulong[] handles, uint count, IntPtr start, IntPtr end);

    [DllImport("advapi32")]
    public static extern int CloseTrace(ulong trace);

    [StructLayout(LayoutKind.Sequential)]
    public struct WnodeHeader
    {
        public uint BufferSize;
        public uint ProviderId;
        public ulong HistoricalContext;
        public long TimeStamp;
        public Guid Guid;
        public uint ClientContext;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TraceProperties
    {
        public WnodeHeader Wnode;
        public uint BufferSize, MinimumBuffers, MaximumBuffers, MaximumFileSize, LogFileMode, FlushTimer, EnableFlags;
        public int AgeLimit;
        public uint NumberOfBuffers, FreeBuffers, EventsLost, BuffersWritten, LogBuffersLost, RealTimeBuffersLost;
        public IntPtr LoggerThreadId;
        public uint LogFileNameOffset, LoggerNameOffset;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EventTraceHeader
    {
        public ushort Size;
        public byte HeaderType, MarkerFlags;
        public uint Version, ThreadId, ProcessId;
        public long TimeStamp;
        public Guid Guid;
        public uint KernelTime, UserTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EventTrace
    {
        public EventTraceHeader Header;
        public uint InstanceId, ParentInstanceId;
        public Guid ParentGuid;
        public IntPtr MofData;
        public uint MofLength, ClientContext;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SystemTime
    {
        public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct TimeZoneInformation
    {
        public int Bias;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string StandardName;
        public SystemTime StandardDate;
        public int StandardBias;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DaylightName;
        public SystemTime DaylightDate;
        public int DaylightBias;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TraceLogfileHeader
    {
        public uint BufferSize, Version, ProviderVersion, NumberOfProcessors;
        public long EndTime;
        public uint TimerResolution, MaximumFileSize, LogFileMode, BuffersWritten;
        public Guid LogInstanceGuid;
        public IntPtr LoggerName, LogFileName;
        public TimeZoneInformation TimeZone;
        public long BootTime, PerfFreq, StartTime;
        public uint ReservedFlags, BuffersLost;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct EventTraceLogfile
    {
        public string? LogFileName;
        public string LoggerName;
        public long CurrentTime;
        public uint BuffersRead;
        public uint ProcessTraceMode;
        public EventTrace CurrentEvent;
        public TraceLogfileHeader LogfileHeader;
        public IntPtr BufferCallback;
        public uint BufferSize, Filled, EventsLost;
        public IntPtr EventRecordCallback;
        public uint IsKernelTrace;
        public IntPtr Context;
    }

    #endregion
}
