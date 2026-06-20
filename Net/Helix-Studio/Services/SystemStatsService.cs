using System.Runtime.InteropServices;
using System.Windows.Threading;
using Helix_Studio.Models;

namespace Helix_Studio.Services;

public sealed class SystemStatsService
{
    private readonly SystemStats _stats;
    private readonly DispatcherTimer _timer;
    private ulong _prevIdle, _prevKernel, _prevUser;
    private bool _hasPrev;

    private AppSettingsService? _appSettings;
    private EventLogService? _eventLog;
    private DateTime _cpuAlertCooldown = DateTime.MinValue;
    private DateTime _memAlertCooldown = DateTime.MinValue;

    public SystemStatsService(SystemStats stats)
    {
        _stats = stats;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Sample();
    }

    public void ConfigureAlerts(AppSettingsService appSettings, EventLogService eventLog)
    {
        _appSettings = appSettings;
        _eventLog = eventLog;
    }

    public void Start()
    {
        Sample();
        _timer.Start();
    }

    private void Sample()
    {
        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref mem))
        {
            _stats.MemoryTotalMb = mem.ullTotalPhys / (1024.0 * 1024.0);
            _stats.MemoryUsedMb = (mem.ullTotalPhys - mem.ullAvailPhys) / (1024.0 * 1024.0);
        }

        if (GetSystemTimes(out var idle, out var kernel, out var user))
        {
            ulong i = idle.ToUInt64(), k = kernel.ToUInt64(), u = user.ToUInt64();
            if (_hasPrev)
            {
                ulong dIdle = i - _prevIdle;
                ulong total = (k - _prevKernel) + (u - _prevUser);
                if (total > 0)
                    _stats.CpuUsagePercent = (1.0 - (double)dIdle / total) * 100.0;
            }
            _prevIdle = i; _prevKernel = k; _prevUser = u;
            _hasPrev = true;
        }

        CheckAlerts();
    }

    private void CheckAlerts()
    {
        if (_appSettings is null || _eventLog is null) return;
        var s = _appSettings.Settings;
        var now = DateTime.UtcNow;

        if (s.CpuAlertEnabled && _stats.CpuUsagePercent > s.CpuAlertThreshold
            && (now - _cpuAlertCooldown).TotalSeconds >= 60)
        {
            _cpuAlertCooldown = now;
            _eventLog.Record(EventLevel.Warning, EventCategory.System, "資源監控",
                $"CPU 使用率超過警報閾值（目前：{_stats.CpuUsagePercent:F1}%，閾值：{s.CpuAlertThreshold}%）");
        }

        if (s.MemoryAlertEnabled && _stats.MemoryTotalMb > 0)
        {
            var memPercent = _stats.MemoryUsedMb / _stats.MemoryTotalMb * 100.0;
            if (memPercent > s.MemoryAlertThreshold
                && (now - _memAlertCooldown).TotalSeconds >= 60)
            {
                _memAlertCooldown = now;
                _eventLog.Record(EventLevel.Warning, EventCategory.System, "資源監控",
                    $"記憶體使用率超過警報閾值（目前：{memPercent:F1}%，閾值：{s.MemoryAlertThreshold}%）");
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint Low;
        public uint High;
        public readonly ulong ToUInt64() => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
