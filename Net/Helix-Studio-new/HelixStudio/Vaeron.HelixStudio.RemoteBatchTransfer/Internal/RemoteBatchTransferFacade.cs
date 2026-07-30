using Vaeron.Kernel;

namespace Vaeron.HelixStudio.RemoteBatchTransfer.Internal;

internal sealed class RemoteBatchTransferFacade(RemoteBatchTransferService service) : IRemoteBatchTransferFacade
{
    public event Action? Changed
    {
        add => service.Changed += value;
        remove => service.Changed -= value;
    }

    public Task<IReadOnlyList<RemoteConnectionSnapshot>> LoadConnectionListAsync() => service.LoadConnectionListAsync();

    public Task<RemoteConnectionSnapshot?> LoadConnectionAsync(int id) => service.LoadConnectionAsync(id);

    public IReadOnlyList<string> LoadDriveRoots() => service.LoadDriveRoots();

    public IReadOnlyList<string> LoadSubdirectories(string path) => service.LoadSubdirectories(path);

    public IReadOnlyList<TransferTaskSnapshot> LoadSessionTasks() => service.LoadSessionTasks();

    public Task<Result> CreateConnectionAsync(CreateConnectionRequest request) => service.CreateConnectionAsync(request);

    public Task<Result> DeleteConnectionAsync(int id) => service.DeleteConnectionAsync(id);

    public Task<Result> TestConnectionAsync(int id) => service.TestConnectionAsync(id);

    public Task<Result> UpdateConnectionAsync(UpdateConnectionRequest request) => service.UpdateConnectionAsync(request);

    public Task<TransferTaskSnapshot> DispatchTaskAsync(DispatchTaskRequest request) => service.DispatchTaskAsync(request);
}
