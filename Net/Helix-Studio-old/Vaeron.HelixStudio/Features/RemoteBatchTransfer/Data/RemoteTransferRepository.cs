namespace Vaeron.HelixStudio.Features.RemoteBatchTransfer.Data;

/// <summary>遠端連線測試與檔案傳輸（傳輸協定尚未決定）</summary>
public sealed class RemoteTransferRepository
{
    private const string NotDecidedMessage = "尚未決定傳輸協定（SMB／SSH／自訂 Agent 待選），無法連線";

    public Task<(bool success, string message)> TestConnectionAsync(string host, int port)
        => Task.FromResult((false, NotDecidedMessage));

    public Task<(bool success, string message)> TransferFileAsync(
        string host, int port, string sourcePath, string destPath, bool move)
        => Task.FromResult((false, NotDecidedMessage));
}
