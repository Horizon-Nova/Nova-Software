using System.Diagnostics;
using System.IO;
using Vaeron.Kernel;

namespace Vaeron.HelixStudio.AppsManagement.Internal;

internal enum ToolRuntimeStatus { Stopped, Starting, Running, Unresponsive, Error }

internal sealed record ToolRuntimeState(ToolRuntimeStatus Status, double CpuPercent, double MemoryMb, nint WindowHandle);

internal sealed record StartToolRequest(int ToolId, string EntryPath, string Args);

internal sealed record WaitForMainWindowRequest(int ToolId, int MaxAttempts = 100, int DelayMs = 100);

/// <summary>子行程生命週期管理（EXE + Job Object）</summary>
internal sealed class ToolProcessStore
{
    private sealed class RunningEntry
    {
        public required Process Process { get; init; }
        public required JobObjectHandle? Job { get; init; }
        public required DateTimeOffset StartedAt { get; init; }
        public TimeSpan LastCpuTime { get; set; }
        public DateTimeOffset LastSampledAt { get; set; }
    }

    private readonly Dictionary<int, RunningEntry> _running = [];

    public bool IsRunning(int toolId) => _running.TryGetValue(toolId, out var e) && !e.Process.HasExited;

    public DateTimeOffset? GetStartedAt(int toolId) => _running.TryGetValue(toolId, out var e) ? e.StartedAt : null;

    /// <summary>啟動子行程</summary>
    public Result Start(StartToolRequest request)
    {
        if (IsRunning(request.ToolId)) return Result.Fail(ResultKind.Conflict, "已在執行中");
        if (!File.Exists(request.EntryPath)) return Result.Fail(ResultKind.NotFound, "找不到執行檔，工具可能未完整安裝");

        var psi = new ProcessStartInfo
        {
            FileName = request.EntryPath,
            Arguments = request.Args,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(request.EntryPath) ?? ".",
        };

        var process = Process.Start(psi);
        if (process is null) return Result.Fail(ResultKind.InfrastructureError, "無法啟動行程");

        var job = JobObjectHandle.Create();
        job?.AssignProcess(process.Handle);

        _running[request.ToolId] = new RunningEntry
        {
            Process = process,
            Job = job,
            StartedAt = DateTimeOffset.UtcNow,
            LastCpuTime = TimeSpan.Zero,
            LastSampledAt = DateTimeOffset.UtcNow,
        };
        return new Result { Kind = ResultKind.Success, Message = "已啟動" };
    }

    /// <summary>輪詢主視窗 HWND，最多等待 maxAttempts*delayMs 毫秒。找不到回傳 0。</summary>
    public async Task<nint> WaitForMainWindowAsync(WaitForMainWindowRequest request)
    {
        if (!_running.TryGetValue(request.ToolId, out var entry)) return nint.Zero;

        for (var i = 0; i < request.MaxAttempts; i++)
        {
            if (entry.Process.HasExited) return nint.Zero;
            entry.Process.Refresh();
            if (entry.Process.MainWindowHandle != nint.Zero)
            {
                await Task.Delay(350);
                return entry.Process.MainWindowHandle;
            }
            await Task.Delay(request.DelayMs);
        }
        return nint.Zero;
    }

    public async Task<Result> StopAsync(int toolId)
    {
        if (!_running.TryGetValue(toolId, out var entry)) return Result.Fail(ResultKind.ValidationFailed, "尚未執行");

        if (!entry.Process.HasExited)
        {
            entry.Process.CloseMainWindow();
            var exited = await WaitForExitAsync(entry.Process, TimeSpan.FromSeconds(3));
            if (!exited) entry.Job?.TerminateAll();
        }

        entry.Job?.Dispose();
        _running.Remove(toolId);
        return new Result { Kind = ResultKind.Success, Message = "已停止" };
    }

    /// <summary>取得目前執行狀態＋CPU/記憶體取樣。已結束的行程會被清出 _running（呼叫方應視為 Stopped）。</summary>
    public ToolRuntimeState GetRuntimeState(int toolId)
    {
        if (!_running.TryGetValue(toolId, out var entry))
            return new ToolRuntimeState(ToolRuntimeStatus.Stopped, 0, 0, nint.Zero);

        entry.Process.Refresh();
        if (entry.Process.HasExited)
        {
            _running.Remove(toolId);
            return new ToolRuntimeState(ToolRuntimeStatus.Stopped, 0, 0, nint.Zero);
        }

        var now = DateTimeOffset.UtcNow;
        var elapsed = (now - entry.LastSampledAt).TotalSeconds;
        var cpuDelta = (entry.Process.TotalProcessorTime - entry.LastCpuTime).TotalMilliseconds;
        var cpuPercent = elapsed > 0.05 ? Math.Clamp(cpuDelta / elapsed / 10.0 / Environment.ProcessorCount, 0, 100) : 0;

        entry.LastCpuTime = entry.Process.TotalProcessorTime;
        entry.LastSampledAt = now;

        var status = entry.Process.MainWindowHandle != nint.Zero || (now - entry.StartedAt) > TimeSpan.FromSeconds(10)
            ? ToolRuntimeStatus.Running
            : ToolRuntimeStatus.Starting;

        return new ToolRuntimeState(status, cpuPercent, entry.Process.WorkingSet64 / 1024.0 / 1024.0, entry.Process.MainWindowHandle);
    }

    /// <summary>App 關閉時清理所有仍在執行的子行程</summary>
    public void DisposeAllRunning()
    {
        foreach (var entry in _running.Values)
        {
            if (!entry.Process.HasExited) entry.Job?.TerminateAll();
            entry.Job?.Dispose();
        }
        _running.Clear();
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        var start = DateTimeOffset.UtcNow;
        while (!process.HasExited && DateTimeOffset.UtcNow - start < timeout)
            await Task.Delay(100);
        return process.HasExited;
    }
}
