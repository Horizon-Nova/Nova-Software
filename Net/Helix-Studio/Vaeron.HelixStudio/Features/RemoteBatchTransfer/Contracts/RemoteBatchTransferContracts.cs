namespace Vaeron.HelixStudio.Features.RemoteBatchTransfer.Contracts;

public enum AuthMethod { Password, Key }
public enum ConnectionStatus { Disconnected, Testing, Connected }
public enum TransferAction { Copy, Move }
public enum TransferTargetStatus { Pending, Transferring, Failed, Completed, Cancelled }

public sealed record RemoteConnectionSnapshot
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Host { get; init; }
    public required int Port { get; init; }
    public required AuthMethod AuthMethod { get; init; }
    public required string Account { get; init; }
    public required ConnectionStatus Status { get; init; }
    public string? LastResultMessage { get; init; }
    public DateTimeOffset? LastResultTime { get; init; }
}

public sealed record CreateConnectionRequest
{
    public required string Name { get; init; }
    public required string Host { get; init; }
    public required int Port { get; init; }
    public required AuthMethod AuthMethod { get; init; }
    public string Account { get; init; } = "";
}

public sealed record TransferTargetSnapshot
{
    public required int ConnectionId { get; init; }
    public required string Name { get; init; }
    public required string DestPath { get; init; }
    public required TransferTargetStatus Status { get; init; }
    public string? Message { get; init; }
}

public sealed record TransferTaskSnapshot
{
    public required Guid Id { get; init; }
    public required TransferAction Action { get; init; }
    public required string SourcePath { get; init; }
    public required string DefaultDestPath { get; init; }
    public required DateTimeOffset DispatchedAt { get; init; }
    public required IReadOnlyList<TransferTargetSnapshot> Targets { get; init; }
}
