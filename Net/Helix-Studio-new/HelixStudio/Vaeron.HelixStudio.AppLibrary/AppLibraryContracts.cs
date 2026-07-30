namespace Vaeron.HelixStudio.AppLibrary;

public sealed record LibraryCategorySnapshot
{
    public required string Id { get; init; }
    public required string Label { get; init; }
}

public sealed record LibraryAppSnapshot
{
    public required string CatalogId { get; init; }
    public required string Name { get; init; }
    public required string CategoryId { get; init; }
    public required string Description { get; init; }
    public required string Version { get; init; }
    public required string Developer { get; init; }
    public required string FileSize { get; init; }
    public required string Requirements { get; init; }
    public required DateTimeOffset LastUpdated { get; init; }
    public string? Readme { get; init; }
    public required bool IsBuiltIn { get; init; }
    public required bool IsInstalledInActiveWorkspace { get; init; }
    public required string? InstalledVersion { get; init; }
    public bool IsUpdateAvailable => InstalledVersion is not null && InstalledVersion != Version;
    /// <summary>已安裝且無更新可用</summary>
    public bool IsInstalledWithoutUpdate => IsInstalledInActiveWorkspace && !IsUpdateAvailable;
    public string UpdateBadgeText => $"有更新可用（已裝 {InstalledVersion}）";
}

public sealed record ImportToolRequest
{
    public required string FilePath { get; init; }
    /// <summary>目前作用中的工作區</summary>
    public int? WorkspaceId { get; init; }
}

public sealed record AddToWorkspaceRequest(string CatalogId, int WorkspaceId);

public sealed record RemoveFromWorkspaceRequest(string AppName, int WorkspaceId);
