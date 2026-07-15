namespace Vaeron.HelixStudio.Features.RemoteBatchTransfer.Data;

/// <summary>遠端連線測試與檔案傳輸的實際執行——依與使用者的確認（見 RemoteBatchTransfer.md 藍圖），
/// 傳輸協定尚未決定（SMB／SSH／自訂 Agent 待選），方法簽名已定稿，本體誠實回傳「尚未決定」，
/// 不用 blueprint 原型的隨機成功/失敗模擬冒充真實行為。</summary>
public sealed class RemoteTransferRepository
{
    private const string NotDecidedMessage = "尚未決定傳輸協定（SMB／SSH／自訂 Agent 待選），無法連線";

    public Task<(bool success, string message)> TestConnectionAsync(string host, int port)
        => Task.FromResult((false, NotDecidedMessage));

    public Task<(bool success, string message)> TransferFileAsync(
        string host, int port, string sourcePath, string destPath, bool move)
        => Task.FromResult((false, NotDecidedMessage));
}
