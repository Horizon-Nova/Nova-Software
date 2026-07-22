using Vaeron.HelixStudio.Features.EventLog.Contracts;
using Vaeron.HelixStudio.Features.EventLog.Data;
using Vaeron.SharedKernel;

namespace Vaeron.HelixStudio.Features.EventLog;

public sealed class EventLogDomain(EventLogRepository repo)
{
    #region 事件中繼

    public event Action? Changed;

    #endregion

    #region 統一的查詢方法

    public async Task<IReadOnlyList<EventEntrySnapshot>> LoadEventListAsync(int workspaceId)
        => (await repo.QueryEventListAsync(workspaceId)).Select(ToSnapshot).ToList();

    public async Task<LogSettingsSnapshot> LoadLogSettingsAsync()
    {
        var row = await repo.QueryLogSettingsAsync();
        return new LogSettingsSnapshot
        {
            MinLevel = Enum.Parse<EventSeverity>(row.min_level),
            RetentionDays = row.retention_days,
            ExportPath = row.export_path,
        };
    }

    public async Task<int> LoadUnreadCountAsync(int workspaceId)
    {
        var since = await repo.QueryLastReadAtAsync(workspaceId);
        return await repo.CountUnreadAsync(workspaceId, since);
    }

    #endregion

    #region 基本 CRUD 操作

    public async Task<(bool success, string message)> RecordEventAsync(RecordEventRequest request)
    {
        var settings = await LoadLogSettingsAsync();
        if (request.Severity < settings.MinLevel)
            return ResultContract.Ok("已略過（低於目前日誌等級門檻）");

        await repo.InsertEventAsync(new event_entry
        {
            workspace_id = request.WorkspaceId,
            tool_name = request.ToolName,
            severity = request.Severity.ToString(),
            message = request.Message,
            timestamp = DateTimeOffset.UtcNow,
            command = request.Command,
            exit_code = request.ExitCode,
            output = request.Output,
        });

        Changed?.Invoke();
        return ResultContract.Ok("已記錄");
    }

    public async Task<(bool success, string message)> UpdateLogSettingsAsync(LogSettingsSnapshot settings)
    {
        await repo.InsertLogSettingsAsync(new log_settings
        {
            min_level = settings.MinLevel.ToString(),
            retention_days = settings.RetentionDays,
            export_path = settings.ExportPath,
        });
        Changed?.Invoke();
        return ResultContract.Ok("已更新");
    }

    public async Task<(bool success, string message)> MarkAsReadAsync(int workspaceId)
    {
        await repo.InsertReadMarkerAsync(workspaceId, DateTimeOffset.UtcNow);
        Changed?.Invoke();
        return ResultContract.Ok("已標記已讀");
    }

    /// <summary>刪除工作區時連動清理事件記錄</summary>
    public async Task DeleteEventsByWorkspaceAsync(int workspaceId)
    {
        await repo.DeleteEventsByWorkspaceAsync(workspaceId);
        Changed?.Invoke();
    }

    /// <summary>記錄保留期限清理</summary>
    public async Task ApplyRetentionPolicyAsync()
    {
        var settings = await LoadLogSettingsAsync();
        if (settings.RetentionDays <= 0) return;

        var cutoff = DateTimeOffset.UtcNow.AddDays(-settings.RetentionDays);
        var deleted = await repo.DeleteEventsOlderThanAsync(cutoff);
        if (deleted > 0) Changed?.Invoke();
    }

    #endregion

    private static EventEntrySnapshot ToSnapshot(event_entry x) => new()
    {
        Id = x.id,
        ToolName = x.tool_name,
        Severity = Enum.Parse<EventSeverity>(x.severity),
        Message = x.message,
        Timestamp = x.timestamp,
        Command = x.command,
        ExitCode = x.exit_code,
        Output = x.output,
    };
}
