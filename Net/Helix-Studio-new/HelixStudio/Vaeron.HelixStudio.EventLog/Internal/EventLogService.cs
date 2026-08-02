using Vaeron.HelixStudio.EventLog.Internal.Models;
using Vaeron.Kernel;

namespace Vaeron.HelixStudio.EventLog.Internal;

internal sealed class EventLogService(EventLogStore store)
{
    public event Action? Changed;

    public async Task<IReadOnlyList<EventEntrySnapshot>> LoadEventListAsync(int workspaceId)
        => (await store.QueryEventListAsync(workspaceId)).Select(ToSnapshot).ToList();

    public async Task<LogSettingsSnapshot> LoadLogSettingsAsync()
    {
        var row = await store.QueryLogSettingsAsync();
        return new LogSettingsSnapshot
        {
            MinLevel = Enum.Parse<EventSeverity>(row.min_level),
            RetentionDays = row.retention_days,
            ExportPath = row.export_path,
        };
    }

    public async Task<int> LoadUnreadCountAsync(int workspaceId)
    {
        var since = await store.QueryLastReadAtAsync(workspaceId);
        return await store.CountUnreadAsync(workspaceId, since);
    }

    public async Task<Result> RecordEventAsync(RecordEventRequest request)
    {
        var settings = await LoadLogSettingsAsync();
        if (request.Severity < settings.MinLevel)
            return Ok("已略過（低於目前日誌等級門檻）");

        await store.InsertEventAsync(new EventEntryRow
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
        return Ok("已記錄");
    }

    /// <summary>供 EventLogAuditSink 使用：FacadeAuditProxy 攔截「全部」Feature 的「全部」方法呼叫，
    /// 絕大多數是成功的 Function 之間互動（Info 分層）——這不是使用者需要在資料庫看到的內容，
    /// 故自動攔截寫入固定排除 Info，只留 Warning／Error（實際失敗或例外）；使用者若想再收斂
    /// （例如只留 Error），到「設定 > 日誌」把 MinLevel 調更高即可套用到這裡，兩者共用同一套
    /// 既有分層，不必另外寫一套判斷邏輯。
    ///
    /// 仍然刻意不觸發 Changed：MainShellViewModel.ApplyRefreshStatusBarAsync 這類會被 EventsChanged
    /// 觸發、而它自己又會呼叫多個會被攔截的 Facade 方法的處理常式，若這裡也觸發 Changed，
    /// 「攔截寫入→觸發 Changed→UI 重新整理→呼叫更多會被攔截的方法→再次觸發 Changed」
    /// 會形成指數成長的迴圈（實測：APP 啟動後數十秒內 event 資料表暴增到六千多筆，UI 卡死）。
    /// 不觸發即時推播本來就是本主題既有的非目標（見第 1 段「不做什麼」），此處不是缺陷。</summary>
    public async Task RecordAutomaticEventAsync(RecordEventRequest request)
    {
        if (request.Severity == EventSeverity.Info) return;

        var settings = await LoadLogSettingsAsync();
        if (request.Severity < settings.MinLevel) return;

        await store.InsertEventAsync(new EventEntryRow
        {
            workspace_id = request.WorkspaceId,
            tool_name = request.ToolName,
            severity = request.Severity.ToString(),
            message = request.Message,
            timestamp = DateTimeOffset.UtcNow,
        });
    }

    public async Task<Result> UpdateLogSettingsAsync(LogSettingsSnapshot settings)
    {
        await store.InsertLogSettingsAsync(new LogSettingsRow
        {
            min_level = settings.MinLevel.ToString(),
            retention_days = settings.RetentionDays,
            export_path = settings.ExportPath,
        });
        Changed?.Invoke();
        return Ok("已更新");
    }

    public async Task<Result> MarkAsReadAsync(int workspaceId)
    {
        await store.InsertReadMarkerAsync(workspaceId, DateTimeOffset.UtcNow);
        Changed?.Invoke();
        return Ok("已標記已讀");
    }

    /// <summary>刪除工作區時連動清理事件記錄</summary>
    public async Task DeleteEventsByWorkspaceAsync(int workspaceId)
    {
        await store.DeleteEventsByWorkspaceAsync(workspaceId);
        Changed?.Invoke();
    }

    /// <summary>記錄保留期限清理</summary>
    public async Task ApplyRetentionPolicyAsync()
    {
        var settings = await LoadLogSettingsAsync();
        if (settings.RetentionDays <= 0) return;

        var cutoff = DateTimeOffset.UtcNow.AddDays(-settings.RetentionDays);
        var deleted = await store.DeleteEventsOlderThanAsync(cutoff);
        if (deleted > 0) Changed?.Invoke();
    }

    private static Result Ok(string message) => new() { Kind = ResultKind.Success, Message = message };

    private static EventEntrySnapshot ToSnapshot(EventEntryRow x) => new()
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
