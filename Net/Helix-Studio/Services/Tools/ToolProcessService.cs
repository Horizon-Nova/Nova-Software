using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Helix_Studio.Infrastructure;
using Helix_Studio.Interop;
using Helix_Studio.Models;
using Helix_Studio.Services;

namespace Helix_Studio.Services.Tools;

/// <summary>執行中的工具：程序 + Job Object + 主視窗 HWND + 工具目錄。</summary>
public sealed class RunningTool
{
    public required string ToolId   { get; init; }
    public required string Name     { get; init; }
    public required string ToolDir  { get; init; }
    public required Process Process { get; init; }
    public required JobObjectHandle Job { get; init; }
    public nint WindowHandle { get; set; }
}

/// <summary>
/// 工具程序執行（FL-12）：
/// 啟動前寫入 tool-context.json；執行期間每秒讀取 tool-status.json；
/// 停止時先送關閉訊號、等待 3 秒、再強制終止，並刪除 tool-status.json。
/// </summary>
public sealed class ToolProcessService : IDisposable
{
    private readonly EventLogService _eventLog;
    private readonly Dictionary<string, RunningTool> _running = new();
    private Timer? _pollTimer;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── 事件 ──────────────────────────────────────────────

    /// <summary>工具非預期結束（崩潰/自行關閉）時觸發，參數為 toolId。</summary>
    public event Action<string>? ToolExited;

    /// <summary>工具主視窗就緒時觸發（toolId, hwnd），供 UI 嵌入。背景執行緒呼叫。</summary>
    public event Action<string, nint>? WindowReady;

    /// <summary>tool-status.json 輪詢後狀態更新（toolId, AppStatus, message）。UI 執行緒外呼叫。</summary>
    public event Action<string, AppStatus, string>? ToolStatusChanged;

    public ToolProcessService(EventLogService eventLog) => _eventLog = eventLog;

    // ── 查詢 ──────────────────────────────────────────────

    public bool IsRunning(string toolId) => _running.ContainsKey(toolId);

    public nint GetWindowHandle(string toolId)
        => _running.TryGetValue(toolId, out var t) ? t.WindowHandle : nint.Zero;

    /// <summary>目前執行中工具的 (toolId, 程序) 快照，供資源取樣使用。</summary>
    public IReadOnlyList<(string ToolId, Process Process)> SnapshotRunning()
        => _running.Values.Select(r => (r.ToolId, r.Process)).ToList();

    // ── 啟動 ──────────────────────────────────────────────

    /// <summary>
    /// 啟動工具（FL-12 N-002 → N-003）。
    /// 先寫入 tool-context.json，再以子程序啟動，最後背景等待主視窗。
    /// </summary>
    public void Start(string workspaceId, InstalledTool tool)
    {
        if (_running.TryGetValue(tool.Id, out var existing))
        {
            if (existing.WindowHandle != nint.Zero)
                WindowReady?.Invoke(tool.Id, existing.WindowHandle);
            return;
        }

        var toolDir   = AppPathProvider.ToolDir(workspaceId, tool.Id);
        var entryPath = Path.Combine(toolDir, tool.Manifest.Entry);
        if (!File.Exists(entryPath))
        {
            _eventLog.Record(EventLevel.Error, EventCategory.Runtime, tool.Name,
                $"啟動失敗：找不到進入點 {tool.Manifest.Entry}");
            throw new FileNotFoundException("找不到工具進入點", entryPath);
        }

        // N-002：寫入 tool-context.json（WorkingDirectory = toolDir，工具以相對路徑讀取）
        WriteToolContext(toolDir, workspaceId);

        var psi = new ProcessStartInfo
        {
            FileName         = entryPath,
            WorkingDirectory = toolDir,
            UseShellExecute  = false,
        };
        foreach (var arg in tool.Settings.Args)
            psi.ArgumentList.Add(arg);

        var process = Process.Start(psi) ?? throw new InvalidOperationException("無法啟動工具程序。");

        var job = new JobObjectHandle();
        job.Assign(process.Handle);

        var running = new RunningTool
        {
            ToolId  = tool.Id,
            Name    = tool.Name,
            ToolDir = toolDir,
            Process = process,
            Job     = job,
        };
        _running[tool.Id] = running;

        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => OnProcessExited(tool.Id);
        _eventLog.Record(EventLevel.Info, EventCategory.Runtime, tool.Name, "已啟動");

        // 程序已建立，進入啟動中狀態：等待工具回報 running 後轉為 Running
        ToolStatusChanged?.Invoke(tool.Id, AppStatus.Starting, string.Empty);

        EnsurePollTimer();

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

    // ── 停止 ──────────────────────────────────────────────

    /// <summary>正常停止工具（FL-12 N-005）：先送關閉訊號，最多等 3 秒，再強制終止。</summary>
    public void Stop(string toolId)
    {
        if (!_running.Remove(toolId, out var running)) return;

        if (!running.Process.HasExited)
        {
            running.Process.CloseMainWindow();
            running.Process.WaitForExit(3000);
            if (!running.Process.HasExited)
                running.Job.TerminateAll();
        }

        CleanupRunning(running);
        _eventLog.Record(EventLevel.Info, EventCategory.Runtime, running.Name, "已停止");
        StopPollTimerIfIdle();
    }

    public void StopAll()
    {
        foreach (var id in _running.Keys.ToList())
            Stop(id);
    }

    // ── IDisposable ───────────────────────────────────────

    public void Dispose()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
        foreach (var r in _running.Values)
            CleanupRunning(r, deleteStatusFile: false);
        _running.Clear();
    }

    // ── 內部：程序結束處理 ────────────────────────────────

    private void OnProcessExited(string toolId)
    {
        if (!_running.Remove(toolId, out var running)) return;

        CleanupRunning(running);
        _eventLog.Record(EventLevel.Warning, EventCategory.Runtime, running.Name,
            "非預期結束（崩潰或自行關閉）");
        ToolExited?.Invoke(toolId);
        StopPollTimerIfIdle();
    }

    // ── 內部：tool-status.json 輪詢 ──────────────────────

    private void EnsurePollTimer()
    {
        if (_pollTimer != null) return;
        _pollTimer = new Timer(PollAllStatuses, null,
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private void StopPollTimerIfIdle()
    {
        if (_running.Count != 0) return;
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    private void PollAllStatuses(object? _)
    {
        foreach (var (toolId, running) in _running.ToList())
        {
            if (running.Process.HasExited) continue;

            var statusPath = Path.Combine(running.ToolDir, "tool-status.json");
            if (!File.Exists(statusPath))
            {
                // 工具已啟動但尚未產生 tool-status.json → 視為執行中（無狀態回報）
                ToolStatusChanged?.Invoke(toolId, AppStatus.Running, string.Empty);
                continue;
            }

            try
            {
                var json = File.ReadAllText(statusPath, Encoding.UTF8);
                var dto  = JsonSerializer.Deserialize<ToolStatusDto>(json, JsonOpts);
                if (dto is null) continue;

                AppStatus appStatus;
                var message = dto.Message ?? string.Empty;

                // 心跳逾時（> 5 秒）→ 視為無回應（以 Error 顯示）
                if (DateTime.TryParse(dto.LastHeartbeat, out var heartbeat) &&
                    (DateTime.UtcNow - heartbeat).TotalSeconds > 5)
                {
                    appStatus = AppStatus.Error;
                    message   = "無回應";
                }
                else
                {
                    appStatus = dto.Status switch
                    {
                        "running" => AppStatus.Running,
                        "idle"    => AppStatus.Running,
                        "error"   => AppStatus.Error,
                        "locked"  => AppStatus.Locked,
                        _         => AppStatus.Running,
                    };
                }

                ToolStatusChanged?.Invoke(toolId, appStatus, message);
            }
            catch { /* 讀取失敗時略過，下次再試 */ }
        }
    }

    // ── 內部：主視窗等待 ──────────────────────────────────

    private static nint WaitForMainWindow(Process process)
    {
        for (int i = 0; i < 100; i++)
        {
            process.Refresh();
            if (process.HasExited) return nint.Zero;
            var h = process.MainWindowHandle;
            if (h != nint.Zero && IsWindowVisible(h))
            {
                Thread.Sleep(350);
                return h;
            }
            Thread.Sleep(100);
        }
        return nint.Zero;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);

    // ── 輔助 ──────────────────────────────────────────────

    private static void WriteToolContext(string toolDir, string workspaceId)
    {
        var dto  = new ToolContextDto(workspaceId, Environment.ProcessId);
        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented        = true,
        });
        File.WriteAllText(Path.Combine(toolDir, "tool-context.json"), json, Encoding.UTF8);
    }

    private static void CleanupRunning(RunningTool running, bool deleteStatusFile = true)
    {
        running.Job.Dispose();
        running.Process.Dispose();

        if (!deleteStatusFile) return;
        var statusPath = Path.Combine(running.ToolDir, "tool-status.json");
        if (File.Exists(statusPath))
            try { File.Delete(statusPath); } catch { /* 刪除失敗時忽略 */ }
    }

    // ── DTO ───────────────────────────────────────────────

    private sealed record ToolContextDto(string WorkspaceId, int HostProcessId);

    private sealed class ToolStatusDto
    {
        [JsonPropertyName("status")]        public string? Status        { get; init; }
        [JsonPropertyName("message")]       public string? Message       { get; init; }
        [JsonPropertyName("lastHeartbeat")] public string? LastHeartbeat { get; init; }
    }
}
