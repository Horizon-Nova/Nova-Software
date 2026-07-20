using System.Text.Json;
using Vaeron.HelixStudio.Features.AppsManagement.Contracts;
using Vaeron.HelixStudio.Features.AppsManagement.Data;
using Vaeron.HelixStudio.Features.EventLog;
using Vaeron.HelixStudio.Features.EventLog.Contracts;
using Vaeron.SharedKernel;

namespace Vaeron.HelixStudio.Features.AppsManagement;

public sealed class AppsManagementDomain(
    InstalledToolsRepository repo,
    ToolProcessRepository processRepo,
    EventLogFacade eventLog)
{
    private const string ModuleUnlockKey = "000000";
    private static readonly TimeSpan UnlockDuration = TimeSpan.FromMinutes(3);

    private readonly Dictionary<int, DateTimeOffset> _unlockExpiry = [];

    #region 事件中繼

    public event Action? Changed;

    #endregion

    #region 統一的查詢方法

    public async Task<IReadOnlyList<InstalledToolSnapshot>> LoadInstalledToolListAsync(int workspaceId)
    {
        var rows = await repo.QueryInstalledToolListAsync(workspaceId);
        return rows.Select(ToSnapshot).ToList();
    }

    #endregion

    #region 基本 CRUD 操作

    public async Task<(bool success, string message)> InstallToolAsync(InstallToolRequest request)
    {
        if (await repo.ExistsByNameAsync(request.WorkspaceId, request.Name))
            return ResultContract.Fail("此工作區已安裝同名工具");

        await repo.InsertInstalledToolAsync(new installed_tool
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
        return ResultContract.Ok("已安裝");
    }

    public async Task<(bool success, string message)> StartToolAsync(int id)
    {
        var tool = await repo.QueryInstalledToolAsync(id);
        if (tool is null) return ResultContract.Fail("找不到該工具");

        var (ok, msg) = processRepo.Start(id, tool.entry_path, tool.args);
        if (!ok)
        {
            await eventLog.RecordEventAsync(new RecordEventRequest
            {
                WorkspaceId = tool.workspace_id,
                ToolName = tool.name,
                Severity = EventSeverity.Error,
                Message = $"啟動失敗：{msg}",
            });
            Changed?.Invoke();
            return ResultContract.Fail(msg);
        }

        _ = processRepo.WaitForMainWindowAsync(id).ContinueWith(_ => Changed?.Invoke());

        await eventLog.RecordEventAsync(new RecordEventRequest
        {
            WorkspaceId = tool.workspace_id,
            ToolName = tool.name,
            Severity = EventSeverity.Info,
            Message = "已啟動",
        });

        Changed?.Invoke();
        return ResultContract.Ok("已啟動");
    }

    public async Task<(bool success, string message)> StopToolAsync(int id)
    {
        var tool = await repo.QueryInstalledToolAsync(id);
        if (tool is null) return ResultContract.Fail("找不到該工具");

        var (ok, msg) = await processRepo.StopAsync(id);

        await eventLog.RecordEventAsync(new RecordEventRequest
        {
            WorkspaceId = tool.workspace_id,
            ToolName = tool.name,
            Severity = ok ? EventSeverity.Info : EventSeverity.Warning,
            Message = ok ? "已停止" : $"停止時發生問題：{msg}",
        });

        Changed?.Invoke();
        return ok ? ResultContract.Ok(msg) : ResultContract.Fail(msg);
    }

    public async Task<(bool success, string message)> UpdateToolSettingsAsync(UpdateToolSettingsRequest request)
    {
        var tool = await repo.QueryInstalledToolAsync(request.Id);
        if (tool is null) return ResultContract.Fail("找不到該工具");

        var locked = ParseLockedFields(tool.locked_fields_json);
        var unlocked = IsUnlocked(request.Id);

        if (locked.Contains("autoStart") && !unlocked && request.AutoStart != tool.auto_start)
            return ResultContract.Fail("此欄位已鎖定，請先輸入模組金鑰解鎖");
        if (locked.Contains("autoRepair") && !unlocked && request.AutoRepair != tool.auto_repair)
            return ResultContract.Fail("此欄位已鎖定，請先輸入模組金鑰解鎖");

        await repo.InsertInstalledToolAsync(new installed_tool
        {
            id = request.Id,
            auto_start = request.AutoStart,
            auto_repair = request.AutoRepair,
            locked_fields_json = tool.locked_fields_json,
        });

        Changed?.Invoke();
        return ResultContract.Ok("已更新");
    }

    public async Task<(bool success, string message)> UnlockFieldsAsync(UnlockFieldsRequest request)
    {
        if (request.ModuleKey != ModuleUnlockKey)
            return ResultContract.Fail("模組金鑰不正確");

        _unlockExpiry[request.Id] = DateTimeOffset.UtcNow.Add(UnlockDuration);
        Changed?.Invoke();
        return await Task.FromResult(ResultContract.Ok("已解鎖，3 分鐘後自動重新鎖定"));
    }

    public async Task<(bool success, string message)> RemoveToolAsync(int id)
    {
        if (processRepo.IsRunning(id))
            return ResultContract.Fail("執行中的工具無法移除，請先停止");

        var tool = await repo.QueryInstalledToolAsync(id);
        var removed = await repo.DeleteInstalledToolAsync(id);
        if (!removed) return ResultContract.Fail("找不到該工具");

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
        return ResultContract.Ok("已移除");
    }

    #endregion

    private bool IsUnlocked(int id)
        => _unlockExpiry.TryGetValue(id, out var expiry) && expiry > DateTimeOffset.UtcNow;

    private static List<string> ParseLockedFields(string json)
        => JsonSerializer.Deserialize<List<string>>(json) ?? [];

    private InstalledToolSnapshot ToSnapshot(installed_tool x)
    {
        var runtime = processRepo.GetRuntimeState(x.id);
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
            LastStarted = processRepo.GetStartedAt(x.id),
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
