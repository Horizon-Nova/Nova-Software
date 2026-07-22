namespace Vaeron.HelixStudio.Features.AppsManagement.Contracts;

public enum ToolState { Stopped, Starting, Running, Error }

public sealed record InstalledToolSnapshot
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required ToolState State { get; init; }
    public required double CpuPercent { get; init; }
    public required double MemoryMb { get; init; }
    public DateTimeOffset? LastStarted { get; init; }
    public required string Version { get; init; }
    public required string Developer { get; init; }
    public required string Description { get; init; }
    public required bool AutoStart { get; init; }
    public required bool AutoRepair { get; init; }
    public required IReadOnlyList<string> LockedFields { get; init; }
    public required bool IsUnlocked { get; init; }
    public DateTimeOffset? UnlockExpiresAt { get; init; }
    public bool IsRunning => State is ToolState.Starting or ToolState.Running;
    public bool IsAutoStartLocked => LockedFields.Contains("autoStart") && !IsUnlocked;
    public bool IsAutoRepairLocked => LockedFields.Contains("autoRepair") && !IsUnlocked;
}

public sealed record InstallToolRequest
{
    public required int WorkspaceId { get; init; }
    public required string CatalogId { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string Developer { get; init; } = "";
    public string Description { get; init; } = "";
    public required string EntryPath { get; init; }
    public string Args { get; init; } = "";
}

public sealed record UpdateToolSettingsRequest
{
    public required int Id { get; init; }
    public required bool AutoStart { get; init; }
    public required bool AutoRepair { get; init; }
}

public sealed record UnlockFieldsRequest
{
    public required int Id { get; init; }
    public required string ModuleKey { get; init; }
}
