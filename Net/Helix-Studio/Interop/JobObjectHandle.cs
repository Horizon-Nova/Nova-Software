using System.Runtime.InteropServices;

namespace Helix_Studio.Interop;

/// <summary>
/// Windows Job Object 封裝：把工具子程序納入隔離單位。
/// 只用 KILL_ON_JOB_CLOSE（此 handle 關閉時，Job 內所有程序連同子孫一併結束，不殘留）。
/// </summary>
public sealed class JobObjectHandle : IDisposable
{
    private nint _handle;
    private bool _disposed;

    public JobObjectHandle()
    {
        _handle = CreateJobObject(nint.Zero, null);
        if (_handle == nint.Zero)
            throw new InvalidOperationException("無法建立 Job Object。");

        // 只用 KILL_ON_JOB_CLOSE（生命週期隔離：Helix 關閉/停止時整樹結束）。
        // 不設 PROCESS_MEMORY 硬性「認可記憶體」上限——那對 GUI 工具太激進會直接被砍掉。
        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
            },
        };

        int length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        nint ptr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(info, ptr, false);
            if (!SetInformationJobObject(_handle, JobObjectExtendedLimitInformation, ptr, (uint)length))
                throw new InvalidOperationException("無法設定 Job Object 限制。");
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    /// <summary>把程序納入此 Job。</summary>
    public void Assign(nint processHandle)
    {
        if (!AssignProcessToJobObject(_handle, processHandle))
            throw new InvalidOperationException("無法將程序納入 Job Object。");
    }

    /// <summary>立即結束 Job 內所有程序（含子孫）。</summary>
    public void TerminateAll()
    {
        if (_handle != nint.Zero)
            TerminateJobObject(_handle, 0);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_handle != nint.Zero)
        {
            CloseHandle(_handle); // KILL_ON_JOB_CLOSE：連帶結束 Job 內程序
            _handle = nint.Zero;
        }
    }

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
    private const int JobObjectExtendedLimitInformation = 9;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateJobObject(nint lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(nint hJob, int infoClass, nint lpInfo, uint cbInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(nint hJob, nint hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateJobObject(nint hJob, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(nint hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }
}
