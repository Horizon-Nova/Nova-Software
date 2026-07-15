using System.Diagnostics;
using System.IO;

namespace Vaeron.HelixStudio.Features.AppsManagement.Data;

public enum ToolRuntimeStatus { Stopped, Starting, Running, Unresponsive, Error }

public sealed record ToolRuntimeState(ToolRuntimeStatus Status, double CpuPercent, double MemoryMb, nint WindowHandle);

/// <summary>子行程掛載機制（EXE + Job Object），對齊
/// .claude/blueprint/執行藍圖/藍圖_外部模組工具隔離架構定案.md 判定的唯一模組擴充機制。
/// 純記憶體內狀態，不碰 DbContext——依 repositories.md「Repository 是技術差異對 Domain
/// 唯一隱藏的地方」的定位，行程/Job Object 互動歸類進本 Feature 的 Data\ 資料夾。</summary>
public sealed class ToolProcessRepository
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

    /// <summary>啟動子行程。用檔案存在檢查取代 try...catch 包 Process.Start——正常情境下
    /// entryPath 不存在是預期會發生的失敗（工具被移除/未安裝完整），提前擋下比拋例外再處理乾淨；
    /// 極少數的競態/權限問題仍可能讓 Process.Start 丟例外，交由 App 層級全域例外處理常式兜底
    /// （見 viewmodels.md／domain.md 對「唯一攔截點在 SharedKernel」的判斷，這裡沒有更好的分層選擇）。</summary>
    public (bool success, string message) Start(int toolId, string entryPath, string args)
    {
        if (IsRunning(toolId)) return (false, "已在執行中");
        if (!File.Exists(entryPath)) return (false, "找不到執行檔，工具可能未完整安裝");

        var psi = new ProcessStartInfo
        {
            FileName = entryPath,
            Arguments = args,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(entryPath) ?? ".",
        };

        var process = Process.Start(psi);
        if (process is null) return (false, "無法啟動行程");

        var job = JobObjectHandle.Create();
        job?.AssignProcess(process.Handle);

        _running[toolId] = new RunningEntry
        {
            Process = process,
            Job = job,
            StartedAt = DateTimeOffset.UtcNow,
            LastCpuTime = TimeSpan.Zero,
            LastSampledAt = DateTimeOffset.UtcNow,
        };
        return (true, "已啟動");
    }

    /// <summary>輪詢主視窗 HWND，最多等待 maxAttempts*delayMs 毫秒。找不到回傳 0。</summary>
    public async Task<nint> WaitForMainWindowAsync(int toolId, int maxAttempts = 100, int delayMs = 100)
    {
        if (!_running.TryGetValue(toolId, out var entry)) return nint.Zero;

        for (var i = 0; i < maxAttempts; i++)
        {
            if (entry.Process.HasExited) return nint.Zero;
            entry.Process.Refresh();
            if (entry.Process.MainWindowHandle != nint.Zero)
            {
                await Task.Delay(350);
                return entry.Process.MainWindowHandle;
            }
            await Task.Delay(delayMs);
        }
        return nint.Zero;
    }

    public async Task<(bool success, string message)> StopAsync(int toolId)
    {
        if (!_running.TryGetValue(toolId, out var entry)) return (false, "尚未執行");

        if (!entry.Process.HasExited)
        {
            entry.Process.CloseMainWindow();
            var exited = await WaitForExitAsync(entry.Process, TimeSpan.FromSeconds(3));
            if (!exited) entry.Job?.TerminateAll();
        }

        entry.Job?.Dispose();
        _running.Remove(toolId);
        return (true, "已停止");
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

    /// <summary>App 關閉時清理所有仍在執行的子行程——不刪除工具自己的狀態檔（App 離開，不是工具離開）。</summary>
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
