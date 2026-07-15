using System.Runtime.InteropServices;

namespace Vaeron.HelixStudio.Features.AppsManagement.Data;

/// <summary>Windows Job Object 包裝——把子行程納入 Job，Job handle 關閉時整個行程樹一併終止，
/// 對齊 .claude/blueprint/執行藍圖/藍圖_外部模組工具隔離架構定案.md 已判定的唯一模組隔離機制。
/// 只設 JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE，不設記憶體/CPU 上限（GUI 工具不宜太激進）。</summary>
public sealed class JobObjectHandle : IDisposable
{
    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x2000;

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr hJob, int infoType, ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateJobObject(IntPtr hJob, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private readonly IntPtr _handle;
    private bool _disposed;

    private JobObjectHandle(IntPtr handle) => _handle = handle;

    /// <summary>建立一個新 Job 並套用 KILL_ON_JOB_CLOSE 限制。失敗回傳 null（呼叫方需自行判斷
    /// IntPtr.Zero 情形，不拋例外——見 repositories.md「禁止 try...catch」，這裡改用回傳值表達失敗，
    /// Win32 API 本身用回傳值/GetLastError 表達失敗，不需要額外包一層例外處理。</summary>
    public static JobObjectHandle? Create()
    {
        var handle = CreateJobObject(IntPtr.Zero, null);
        if (handle == IntPtr.Zero) return null;

        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION { LimitFlags = JobObjectLimitKillOnJobClose },
        };
        var length = (uint)Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        if (!SetInformationJobObject(handle, JobObjectExtendedLimitInformation, ref info, length))
        {
            CloseHandle(handle);
            return null;
        }

        return new JobObjectHandle(handle);
    }

    public bool AssignProcess(IntPtr processHandle) => AssignProcessToJobObject(_handle, processHandle);

    public void TerminateAll() => TerminateJobObject(_handle, 1);

    public void Dispose()
    {
        if (_disposed) return;
        CloseHandle(_handle);
        _disposed = true;
    }
}
