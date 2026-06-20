using System.Diagnostics;
using System.IO;
using Helix_Studio.Infrastructure;
using Helix_Studio.Interop;
using Helix_Studio.Models;
using Helix_Studio.Services;

namespace Helix_Studio.Services.Tools;

/// <summary>執行中的工具：程序 + Job Object + 主視窗 HWND。</summary>
public sealed class RunningTool
{
    public required string ToolId { get; init; }
    public required string Name { get; init; }
    public required Process Process { get; init; }
    public required JobObjectHandle Job { get; init; }
    public nint WindowHandle { get; set; }
}

/// <summary>
/// 工具程序執行：以子程序啟動已安裝工具、納入 Job Object 隔離、取得主視窗 HWND、停止。
/// 實際 UI 嵌入由 EmbeddedWindowHost 處理。
/// </summary>
public sealed class ToolProcessService
{
    private readonly EventLogService _eventLog;
    private readonly Dictionary<string, RunningTool> _running = new();

    /// <summary>工具非預期結束（崩潰/自行關閉）時觸發，參數為 toolId。</summary>
    public event Action<string>? ToolExited;

    /// <summary>工具主視窗就緒時觸發（toolId, hwnd），供 UI 嵌入。背景執行緒呼叫。</summary>
    public event Action<string, nint>? WindowReady;

    public ToolProcessService(EventLogService eventLog) => _eventLog = eventLog;

    public bool IsRunning(string toolId) => _running.ContainsKey(toolId);

    public nint GetWindowHandle(string toolId)
        => _running.TryGetValue(toolId, out var t) ? t.WindowHandle : nint.Zero;

    /// <summary>目前執行中工具的 (toolId, 程序) 快照，供資源取樣使用。</summary>
    public IReadOnlyList<(string ToolId, Process Process)> SnapshotRunning()
        => _running.Values.Select(r => (r.ToolId, r.Process)).ToList();

    /// <summary>
    /// 啟動工具（不阻塞 UI）。程序與 Job 立即建立；主視窗就緒後以背景執行緒透過 WindowReady 通知。
    /// 若已在執行，立即以既有 HWND 觸發 WindowReady。
    /// </summary>
    public void Start(string workspaceId, InstalledTool tool)
    {
        if (_running.TryGetValue(tool.Id, out var existing))
        {
            if (existing.WindowHandle != nint.Zero)
                WindowReady?.Invoke(tool.Id, existing.WindowHandle);
            return;
        }

        var toolDir = AppPathProvider.ToolDir(workspaceId, tool.Id);
        var entryPath = Path.Combine(toolDir, tool.Manifest.Entry);
        if (!File.Exists(entryPath))
        {
            _eventLog.Record(EventLevel.Error, EventCategory.Runtime, tool.Name, $"啟動失敗：找不到進入點 {tool.Manifest.Entry}");
            throw new FileNotFoundException("找不到工具進入點", entryPath);
        }

        var psi = new ProcessStartInfo
        {
            FileName = entryPath,
            WorkingDirectory = toolDir,
            UseShellExecute = false,
        };
        foreach (var arg in tool.Settings.Args)
            psi.ArgumentList.Add(arg);

        var process = Process.Start(psi) ?? throw new InvalidOperationException("無法啟動工具程序。");

        var job = new JobObjectHandle();
        job.Assign(process.Handle);

        var running = new RunningTool { ToolId = tool.Id, Name = tool.Name, Process = process, Job = job };
        _running[tool.Id] = running;

        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => OnProcessExited(tool.Id);
        _eventLog.Record(EventLevel.Info, EventCategory.Runtime, tool.Name, "已啟動");

        // 背景等待主視窗，避免凍結 UI
        Task.Run(() =>
        {
            var hwnd = WaitForMainWindow(process);
            if (hwnd != nint.Zero && _running.ContainsKey(tool.Id))
            {
                running.WindowHandle = hwnd;
                WindowReady?.Invoke(tool.Id, hwnd);
            }
        });
    }

    public void Stop(string toolId)
    {
        if (!_running.Remove(toolId, out var running)) return;
        running.Job.TerminateAll();
        running.Job.Dispose();   // KILL_ON_JOB_CLOSE：連帶清除
        running.Process.Dispose();
        _eventLog.Record(EventLevel.Info, EventCategory.Runtime, running.Name, "已停止");
    }

    public void StopAll()
    {
        foreach (var id in _running.Keys.ToList())
            Stop(id);
    }

    private void OnProcessExited(string toolId)
    {
        // 仍在 _running 代表非經由 Stop()——即工具自行關閉或崩潰
        if (_running.Remove(toolId, out var running))
        {
            running.Job.Dispose();
            running.Process.Dispose();
            _eventLog.Record(EventLevel.Warning, EventCategory.Runtime, running.Name, "非預期結束（崩潰或自行關閉）");
        }
        ToolExited?.Invoke(toolId);
    }

    /// <summary>
    /// 輪詢等待主視窗「建立且可見」（最多約 10 秒），再多等一小段讓 WPF 完成顯示，
    /// 避免過早 reparent 造成嵌入競態（視窗浮在外、宿主空白）。
    /// </summary>
    private static nint WaitForMainWindow(Process process)
    {
        for (int i = 0; i < 100; i++)
        {
            process.Refresh();
            if (process.HasExited) return nint.Zero;
            var h = process.MainWindowHandle;
            if (h != nint.Zero && IsWindowVisible(h))
            {
                Thread.Sleep(350);   // 讓視窗完成顯示與版面
                return h;
            }
            Thread.Sleep(100);
        }
        return nint.Zero;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);
}
