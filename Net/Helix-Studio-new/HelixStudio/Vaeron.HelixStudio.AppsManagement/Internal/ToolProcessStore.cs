using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
        /// <summary>一旦找到就快取——某些工具（如 InSpec-Tool）刻意隱藏啟動再等殼層嵌入時才顯示，
        /// 嵌入後該視窗會多一個父視窗，Win32 對「頂層視窗」的認定就不再成立，
        /// 之後不能再靠重新列舉找到它，必須靠這個快取。</summary>
        public nint CachedWindowHandle { get; set; }
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

    /// <summary>輪詢頂層視窗 HWND，最多等待 maxAttempts*delayMs 毫秒。找不到回傳 0。
    /// 用 EnumWindows 找該行程擁有、還沒有父視窗的頂層視窗，不用 Process.MainWindowHandle——
    /// 後者只認「可見」視窗，找不到刻意隱藏啟動（等殼層嵌入才顯示）的工具視窗。</summary>
    public async Task<nint> WaitForMainWindowAsync(WaitForMainWindowRequest request)
    {
        if (!_running.TryGetValue(request.ToolId, out var entry)) return nint.Zero;

        for (var i = 0; i < request.MaxAttempts; i++)
        {
            if (entry.Process.HasExited) return nint.Zero;
            var hwnd = FindTopLevelWindow(entry.Process.Id);
            if (hwnd != nint.Zero)
            {
                await Task.Delay(350); // 讓視窗完成第一次版面配置，避免嵌入時內容還沒畫出來
                entry.CachedWindowHandle = hwnd;
                return hwnd;
            }
            await Task.Delay(request.DelayMs);
        }
        return nint.Zero;
    }

    /// <summary>列舉該行程擁有、目前沒有父視窗的第一個頂層視窗。刻意不要求可見——
    /// 隱藏啟動、等嵌入才顯示的工具視窗在被找到當下本來就還不可見。</summary>
    private static nint FindTopLevelWindow(int processId)
    {
        var found = nint.Zero;
        EnumWindows((hWnd, _) =>
        {
            GetWindowThreadProcessId(hWnd, out var pid);
            if (pid == (uint)processId && GetParent(hWnd) == nint.Zero)
            {
                found = hWnd;
                return false; // 找到了，停止列舉
            }
            return true;
        }, nint.Zero);
        return found;
    }

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern nint GetParent(nint hWnd);

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

        if (entry.CachedWindowHandle == nint.Zero)
            entry.CachedWindowHandle = FindTopLevelWindow(entry.Process.Id);

        var status = entry.CachedWindowHandle != nint.Zero || (now - entry.StartedAt) > TimeSpan.FromSeconds(10)
            ? ToolRuntimeStatus.Running
            : ToolRuntimeStatus.Starting;

        return new ToolRuntimeState(status, cpuPercent, entry.Process.WorkingSet64 / 1024.0 / 1024.0, entry.CachedWindowHandle);
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
