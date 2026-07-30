using Vaeron.Kernel;

namespace Vaeron.HelixStudio.RemoteBatchTransfer;

/// <summary>無跨 Feature 呼叫方，此介面存在的理由同 AppLibrary／UsbVoltageControl：
/// ViewModel 是公開型別，建構子參數不能小於它的存取範圍（CS0051）。</summary>
public interface IRemoteBatchTransferFacade
{
    public event Action? Changed;

    public Task<IReadOnlyList<RemoteConnectionSnapshot>> LoadConnectionListAsync();

    public Task<RemoteConnectionSnapshot?> LoadConnectionAsync(int id);

    public IReadOnlyList<string> LoadDriveRoots();

    public IReadOnlyList<string> LoadSubdirectories(string path);

    public IReadOnlyList<TransferTaskSnapshot> LoadSessionTasks();

    public Task<Result> CreateConnectionAsync(CreateConnectionRequest request);

    public Task<Result> DeleteConnectionAsync(int id);

    public Task<Result> TestConnectionAsync(int id);

    public Task<Result> UpdateConnectionAsync(UpdateConnectionRequest request);

    /// <summary>發布批次任務（允許部分派送，缺路徑的目標會被跳過）</summary>
    public Task<TransferTaskSnapshot> DispatchTaskAsync(DispatchTaskRequest request);
}
