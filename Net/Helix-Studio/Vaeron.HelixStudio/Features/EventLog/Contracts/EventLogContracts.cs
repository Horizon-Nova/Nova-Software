namespace Vaeron.HelixStudio.Features.EventLog.Contracts;

/// <summary>None 刻意排在最後（最高序位）——「記錄門檻」比較用 `&lt;` 運算子（見
/// EventLogDomain.RecordEventAsync），任何實際事件的嚴重度都小於 None，門檻設 None 時
/// 因此永遠略過記錄，不需要額外的特例分支。</summary>
public enum EventSeverity { Info, Warning, Error, None }

public sealed record EventEntrySnapshot
{
    public required int Id { get; init; }
    public required string ToolName { get; init; }
    public required EventSeverity Severity { get; init; }
    public required string Message { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public string? Command { get; init; }
    public int? ExitCode { get; init; }
    public string? Output { get; init; }
}

public sealed record RecordEventRequest
{
    public required int WorkspaceId { get; init; }
    public required string ToolName { get; init; }
    public required EventSeverity Severity { get; init; }
    public required string Message { get; init; }
    public string? Command { get; init; }
    public int? ExitCode { get; init; }
    public string? Output { get; init; }
}

public sealed record LogSettingsSnapshot
{
    public required EventSeverity MinLevel { get; init; }
    public required int RetentionDays { get; init; }
}
