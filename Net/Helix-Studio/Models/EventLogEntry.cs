namespace Helix_Studio.Models;

/// <summary>事件日誌的一筆記錄。建立後不可變。</summary>
public sealed class EventLogEntry
{
    public required DateTime Timestamp { get; init; }
    public required EventLevel Level { get; init; }

    /// <summary>事件類別；非必填以相容於早期未含此欄位的日誌（預設為 System）。</summary>
    public EventCategory Category { get; init; }

    /// <summary>事件來源（應用程式或子系統名稱）。</summary>
    public required string Source { get; init; }
    public required string Message { get; init; }
}
