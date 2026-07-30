using Vaeron.Kernel;

namespace Vaeron.HelixStudio.RemoteBatchTransfer.Internal;

internal sealed record TestConnectionRequest(string Host, int Port);

internal sealed record TransferFileRequest(string Host, int Port, string SourcePath, string DestPath, bool Move);

/// <summary>遠端連線測試與檔案傳輸（傳輸協定尚未決定）</summary>
internal sealed class RemoteTransferStore
{
    private const string NotDecidedMessage = "尚未決定傳輸協定（SMB／SSH／自訂 Agent 待選），無法連線";

    public Task<Result> TestConnectionAsync(TestConnectionRequest request)
        => Task.FromResult(Result.Fail(ResultKind.Unavailable, NotDecidedMessage));

    public Task<Result> TransferFileAsync(TransferFileRequest request)
        => Task.FromResult(Result.Fail(ResultKind.Unavailable, NotDecidedMessage));
}
