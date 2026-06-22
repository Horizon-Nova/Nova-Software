using System.Diagnostics;
using System.Windows.Threading;
using Helix_Studio.Models;

namespace Helix_Studio.Services.Tools;

public sealed class ProcessSampler
{
    private readonly ToolProcessService _processes;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<string, TimeSpan> _prevCpu = new();
    private readonly Dictionary<string, DateTime> _toolAlertCooldown = new();
    private DateTime _prevSample;

    private AppSettingsService? _appSettings;
    private EventLogService? _eventLog;

    public event Action<string, double, double>? Sampled;

    public ProcessSampler(ToolProcessService processes)
    {
        _processes = processes;
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
        _prevSample = DateTime.UtcNow;
        _timer.Start();
    }

    private void Sample()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _prevSample).TotalSeconds;
        _prevSample = now;
        if (elapsed <= 0) return;

        var live = _processes.SnapshotRunning();
        var liveIds = new HashSet<string>();

        foreach (var (toolId, process) in live)
        {
            liveIds.Add(toolId);
            process.Refresh();
            if (process.HasExited) continue;

            var cpu = process.TotalProcessorTime;
            double percent = 0;
            if (_prevCpu.TryGetValue(toolId, out var prev))
                percent = Math.Clamp((cpu - prev).TotalSeconds / (elapsed * Environment.ProcessorCount) * 100.0, 0, 100);
            _prevCpu[toolId] = cpu;

            var memoryMb = process.WorkingSet64 / (1024.0 * 1024.0);
            Sampled?.Invoke(toolId, percent, memoryMb);

            CheckToolAlert(toolId, process.ProcessName, percent, memoryMb, now);
        }

        foreach (var goneId in _prevCpu.Keys.Where(id => !liveIds.Contains(id)).ToList())
        {
            _prevCpu.Remove(goneId);
            _toolAlertCooldown.Remove(goneId);
        }
    }

    private void CheckToolAlert(string toolId, string toolName, double cpuPercent, double memoryMb, DateTime now)
    {
        if (_appSettings is null || _eventLog is null) return;
        var s = _appSettings.Settings;
        if (!s.ToolLevelAlertsEnabled) return;

        _toolAlertCooldown.TryGetValue(toolId, out var lastAlert);
        if ((now - lastAlert).TotalSeconds < 60) return;

        if (s.CpuAlertEnabled)
        {
            var cpuThreshold = s.CpuAlertThreshold * 0.5;
            if (cpuPercent > cpuThreshold)
            {
                _toolAlertCooldown[toolId] = now;
                _eventLog.Record(EventLevel.Warning, EventCategory.Runtime, toolName,
                    $"CPU 使用率超過警報閾值（目前：{cpuPercent:F1}%，閾值：{cpuThreshold:F0}%）");
                return;
            }
        }

        if (s.MemoryAlertEnabled && memoryMb > 0)
        {
            var totalMb = GetSystemMemoryTotalMb();
            if (totalMb > 0)
            {
                var memThreshold = s.MemoryAlertThreshold * 0.5;
                var memPercent = memoryMb / totalMb * 100.0;
                if (memPercent > memThreshold)
                {
                    _toolAlertCooldown[toolId] = now;
                    _eventLog.Record(EventLevel.Warning, EventCategory.Runtime, toolName,
                        $"記憶體使用率超過警報閾值（目前：{memPercent:F1}%，閾值：{memThreshold:F0}%）");
                }
            }
        }
    }

    private static double GetSystemMemoryTotalMb()
    {
        try
        {
            var mem = new MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MEMORYSTATUSEX>() };
            return GlobalMemoryStatusEx(ref mem) ? mem.ullTotalPhys / (1024.0 * 1024.0) : 0;
        }
        catch { return 0; }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
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

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
