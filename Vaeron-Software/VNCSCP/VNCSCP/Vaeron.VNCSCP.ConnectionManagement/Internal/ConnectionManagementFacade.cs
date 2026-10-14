using Vaeron.Kernel;
using Vaeron.Kernel.Secrets;
using Vaeron.VNCSCP.Internal.Contracts;

namespace Vaeron.VNCSCP.ConnectionManagement.Internal;

/// <summary>只委派，不含邏輯。</summary>
internal sealed class ConnectionManagementFacade(ConnectionManagementService service) : IConnectionManagementFacade
{
    public event Action? Changed
    {
        add => service.Changed += value;
        remove => service.Changed -= value;
    }

    public ValueTask<Result<IReadOnlyList<ConnectionSnapshot>>> LoadConnectionListAsync(
        ConnectionFilter filter, CancellationToken cancellationToken)
        => service.LoadConnectionListAsync(filter, cancellationToken);

    public ValueTask<Result<IReadOnlyList<string>>> LoadGroupNameListAsync(CancellationToken cancellationToken)
        => service.LoadGroupNameListAsync(cancellationToken);

    public ValueTask<Result> CreateConnectionAsync(
        CreateConnectionRequest request, SecretBuffer password, CancellationToken cancellationToken)
        => service.CreateConnectionAsync(request, password, cancellationToken);

    public ValueTask<Result> UpdateConnectionAsync(
        UpdateConnectionRequest request, SecretBuffer? password, CancellationToken cancellationToken)
        => service.UpdateConnectionAsync(request, password, cancellationToken);

    public ValueTask<Result> DeleteConnectionAsync(int id, CancellationToken cancellationToken)
        => service.DeleteConnectionAsync(id, cancellationToken);

    public ValueTask<Result<string>> RevealPasswordAsync(int id, CancellationToken cancellationToken)
        => service.RevealPasswordAsync(id, cancellationToken);

    public ValueTask<Result<ConnectionSnapshot>> TestConnectionAsync(int id, CancellationToken cancellationToken)
        => service.TestConnectionAsync(id, cancellationToken);

    public ValueTask<Result> RenameGroupAsync(RenameGroupRequest request, CancellationToken cancellationToken)
        => service.RenameGroupAsync(request, cancellationToken);

    public ValueTask<Result> UngroupAsync(string name, CancellationToken cancellationToken)
        => service.UngroupAsync(name, cancellationToken);
}
