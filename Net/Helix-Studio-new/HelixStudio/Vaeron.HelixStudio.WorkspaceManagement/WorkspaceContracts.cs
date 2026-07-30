namespace Vaeron.HelixStudio.WorkspaceManagement;

public sealed record WorkspaceSnapshot
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required bool IsActive { get; init; }
}

public sealed record CreateWorkspaceRequest
{
    public required string Name { get; init; }
    public string Description { get; init; } = "";
}

public sealed record RenameWorkspaceRequest
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
}
