namespace Vaeron.HelixStudio.EventLog;

/// <summary>事件嚴重度（None 為最高序位，代表不記錄）</summary>
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
    public bool HasDetails => Command is not null || Output is not null || ExitCode is not null;
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
    public string? ExportPath { get; init; }
}
