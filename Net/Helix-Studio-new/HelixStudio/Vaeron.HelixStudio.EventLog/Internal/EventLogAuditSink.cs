using Vaeron.HelixStudio.Internal.Runtime;

namespace Vaeron.HelixStudio.EventLog.Internal;

/// <summary>把 FacadeAuditProxy 攔截到的每一筆呼叫，額外落地一份到 EventLog 資料庫——
/// 與 AuditLogging（本地文字檔）是兩條平行、互不依賴的路徑，不是「文字檔寫完才寫資料庫」的串接。
/// 沒有作用中工作區時（例如全新安裝、尚未建立任何工作區）本 Sink 沒有合法的歸屬對象可寫,直接略過——
/// 文字檔那邊仍會照樣捕捉到,不受影響。</summary>
internal sealed class EventLogAuditSink(EventLogService service, ActiveWorkspaceRuntime activeWorkspace) : IAuditSink
{
    public Task CaptureAsync(AuditEntry entry)
    {
        var workspaceId = activeWorkspace.CurrentId;
        if (workspaceId is null) return Task.CompletedTask;

        var severity = entry.Cause is not null ? EventSeverity.Error
            : entry.IsFailure ? EventSeverity.Warning
            : EventSeverity.Info;

        return service.RecordAutomaticEventAsync(new RecordEventRequest
        {
            WorkspaceId = workspaceId.Value,
            ToolName = entry.Layer,
            Severity = severity,
            Message = entry.Message,
        });
    }
}
