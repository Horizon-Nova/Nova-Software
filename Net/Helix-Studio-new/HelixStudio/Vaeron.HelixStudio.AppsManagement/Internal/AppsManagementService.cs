using System.Text.Json;
using Vaeron.HelixStudio.AppsManagement.Internal.Models;
using Vaeron.HelixStudio.EventLog;
using Vaeron.Kernel;

namespace Vaeron.HelixStudio.AppsManagement.Internal;

internal sealed class AppsManagementService(
    InstalledToolsStore store,
    ToolProcessStore processStore,
    IEventLogFacade eventLog)
{
    private const string ModuleUnlockKey = "000000";
    private static readonly TimeSpan UnlockDuration = TimeSpan.FromMinutes(3);

    private readonly Dictionary<int, DateTimeOffset> _unlockExpiry = [];

    public event Action? Changed;

    public async Task<IReadOnlyList<InstalledToolSnapshot>> LoadInstalledToolListAsync(int workspaceId)
    {
        var rows = await store.QueryInstalledToolListAsync(workspaceId);
        return rows.Select(ToSnapshot).ToList();
    }

    public async Task<Result> InstallToolAsync(InstallToolRequest request)
    {
        if (await store.ExistsByNameAsync(new ExistsByNameQuery(request.WorkspaceId, request.Name)))
            return Result.Fail(ResultKind.Conflict, "此工作區已安裝同名工具");

        await store.InsertInstalledToolAsync(new InstalledToolRow
        {
            workspace_id = request.WorkspaceId,
            catalog_id = request.CatalogId,
            name = request.Name,
            version = request.Version,
            developer = request.Developer,
            description = request.Description,
            entry_path = request.EntryPath,
            args = request.Args,
            installed_at = DateTimeOffset.UtcNow,
        });

        await eventLog.RecordEventAsync(new RecordEventRequest
        {
            WorkspaceId = request.WorkspaceId,
            ToolName = request.Name,
            Severity = EventSeverity.Info,
            Message = "已安裝",
        });

        Changed?.Invoke();
        return Ok("已安裝");
    }

    public async Task<Result> StartToolAsync(int id)
    {
        var tool = await store.QueryInstalledToolAsync(id);
        if (tool is null) return Result.Fail(ResultKind.NotFound, "找不到該工具");

        var started = processStore.Start(new StartToolRequest(id, tool.entry_path, tool.args));
        if (!started.IsSuccess)
        {
            await eventLog.RecordEventAsync(new RecordEventRequest
            {
                WorkspaceId = tool.workspace_id,
                ToolName = tool.name,
                Severity = EventSeverity.Error,
                Message = $"啟動失敗：{started.Message}",
            });
            Changed?.Invoke();
            return started;
        }

        _ = processStore.WaitForMainWindowAsync(new WaitForMainWindowRequest(id)).ContinueWith(_ => Changed?.Invoke());

        await eventLog.RecordEventAsync(new RecordEventRequest
        {
            WorkspaceId = tool.workspace_id,
            ToolName = tool.name,
            Severity = EventSeverity.Info,
            Message = "已啟動",
        });

        Changed?.Invoke();
        return Ok("已啟動");
    }

    public async Task<Result> StopToolAsync(int id)
    {
        var tool = await store.QueryInstalledToolAsync(id);
        if (tool is null) return Result.Fail(ResultKind.NotFound, "找不到該工具");

        var stopped = await processStore.StopAsync(id);

        await eventLog.RecordEventAsync(new RecordEventRequest
        {
            WorkspaceId = tool.workspace_id,
            ToolName = tool.name,
            Severity = stopped.IsSuccess ? EventSeverity.Info : EventSeverity.Warning,
            Message = stopped.IsSuccess ? "已停止" : $"停止時發生問題：{stopped.Message}",
        });

        Changed?.Invoke();
        return stopped;
    }

    public async Task<Result> UpdateToolSettingsAsync(UpdateToolSettingsRequest request)
    {
        var tool = await store.QueryInstalledToolAsync(request.Id);
        if (tool is null) return Result.Fail(ResultKind.NotFound, "找不到該工具");

        var locked = ParseLockedFields(tool.locked_fields_json);
        var unlocked = IsUnlocked(request.Id);

        if (locked.Contains("autoStart") && !unlocked && request.AutoStart != tool.auto_start)
            return Result.Fail(ResultKind.ValidationFailed, "此欄位已鎖定，請先輸入模組金鑰解鎖");
        if (locked.Contains("autoRepair") && !unlocked && request.AutoRepair != tool.auto_repair)
            return Result.Fail(ResultKind.ValidationFailed, "此欄位已鎖定，請先輸入模組金鑰解鎖");

        await store.InsertInstalledToolAsync(new InstalledToolRow
        {
            id = request.Id,
            auto_start = request.AutoStart,
            auto_repair = request.AutoRepair,
            locked_fields_json = tool.locked_fields_json,
        });

        Changed?.Invoke();
        return Ok("已更新");
    }

    public Task<Result> UnlockFieldsAsync(UnlockFieldsRequest request)
    {
        if (request.ModuleKey != ModuleUnlockKey)
            return Task.FromResult(Result.Fail(ResultKind.ValidationFailed, "模組金鑰不正確"));

        _unlockExpiry[request.Id] = DateTimeOffset.UtcNow.Add(UnlockDuration);
        Changed?.Invoke();
        return Task.FromResult(Ok("已解鎖，3 分鐘後自動重新鎖定"));
    }

    public async Task<Result> RemoveToolAsync(int id)
    {
        if (processStore.IsRunning(id))
            return Result.Fail(ResultKind.Conflict, "執行中的工具無法移除，請先停止");

        var tool = await store.QueryInstalledToolAsync(id);
        var removed = await store.DeleteInstalledToolAsync(id);
        if (!removed) return Result.Fail(ResultKind.NotFound, "找不到該工具");

        if (tool is not null)
        {
            await eventLog.RecordEventAsync(new RecordEventRequest
            {
                WorkspaceId = tool.workspace_id,
                ToolName = tool.name,
                Severity = EventSeverity.Info,
                Message = "已移除",
            });
        }

        Changed?.Invoke();
        return Ok("已移除");
    }

    public async Task<bool> IsToolRunningByNameAsync(IsToolRunningQuery query)
    {
        var list = await LoadInstalledToolListAsync(query.WorkspaceId);
        return list.Any(t => t.Name == query.ToolName && t.IsRunning);
    }

    /// <summary>刪除工作區時連動清理已安裝工具</summary>
    public async Task DeleteToolsByWorkspaceAsync(int workspaceId)
    {
        var tools = await store.QueryInstalledToolListAsync(workspaceId);
        foreach (var tool in tools.Where(t => processStore.IsRunning(t.id)))
            await processStore.StopAsync(tool.id);

        await store.DeleteToolsByWorkspaceAsync(workspaceId);
        Changed?.Invoke();
    }

    public void DisposeAllRunningProcesses() => processStore.DisposeAllRunning();

    private bool IsUnlocked(int id)
        => _unlockExpiry.TryGetValue(id, out var expiry) && expiry > DateTimeOffset.UtcNow;

    private static List<string> ParseLockedFields(string json)
        => JsonSerializer.Deserialize<List<string>>(json) ?? [];

    private static Result Ok(string message) => new() { Kind = ResultKind.Success, Message = message };

    private InstalledToolSnapshot ToSnapshot(InstalledToolRow x)
    {
        var runtime = processStore.GetRuntimeState(x.id);
        var locked = ParseLockedFields(x.locked_fields_json);
        var unlocked = IsUnlocked(x.id);

        return new InstalledToolSnapshot
        {
            Id = x.id,
            Name = x.name,
            State = runtime.Status switch
            {
                ToolRuntimeStatus.Running => ToolState.Running,
                ToolRuntimeStatus.Starting => ToolState.Starting,
                ToolRuntimeStatus.Unresponsive or ToolRuntimeStatus.Error => ToolState.Error,
                _ => ToolState.Stopped,
            },
            CpuPercent = runtime.CpuPercent,
            MemoryMb = runtime.MemoryMb,
            LastStarted = processStore.GetStartedAt(x.id),
            Version = x.version,
            Developer = x.developer,
            Description = x.description,
            AutoStart = x.auto_start,
            AutoRepair = x.auto_repair,
            LockedFields = locked,
            IsUnlocked = unlocked,
            UnlockExpiresAt = _unlockExpiry.TryGetValue(x.id, out var exp) ? exp : null,
        };
    }
}
