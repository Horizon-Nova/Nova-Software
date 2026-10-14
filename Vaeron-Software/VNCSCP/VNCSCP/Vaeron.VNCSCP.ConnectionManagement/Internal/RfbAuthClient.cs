using System.Net.Sockets;
using Vaeron.Kernel;
using Vaeron.Kernel.Secrets;
using Vaeron.VNCSCP.Internal.Contracts;
using Vaeron.VNCSCP.Internal.Rfb;

namespace Vaeron.VNCSCP.ConnectionManagement.Internal;

/// <summary>RFB 測試目標。</summary>
internal sealed record RfbTestTarget(string Ip, int Port);

/// <summary>RFB 測試結果：狀態、給人看的訊息、逐步交握診斷。</summary>
internal sealed record RfbTestOutcome(ConnectionStatus Status, string Message, string Diagnostic);

/// <summary>連線測試：只做到交握與驗證為止，不開任何視窗，不進入畫面更新迴圈。</summary>
internal sealed class RfbAuthClient
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);

    /// <summary>安全結果碼由協定定義，逐碼對應一種連線狀態。</summary>
    private static readonly Dictionary<uint, ConnectionStatus> StatusBySecurityResult = new()
    {
        [0] = ConnectionStatus.Reachable,
        [1] = ConnectionStatus.WrongPassword,
        [2] = ConnectionStatus.Locked
    };

    private static readonly Dictionary<uint, string> MessageBySecurityResult = new()
    {
        [0] = "連線成功",
        [1] = "密碼錯誤",
        [2] = "嘗試次數過多，暫時被鎖定"
    };

    /// <summary>交握失敗的種類各自對應一種要記錄的連線狀態。</summary>
    private static readonly Dictionary<ResultKind, ConnectionStatus> StatusByFailureKind = new()
    {
        [ResultKind.Success] = ConnectionStatus.Reachable,
        [ResultKind.Conflict] = ConnectionStatus.Locked,
        [ResultKind.Unavailable] = ConnectionStatus.Unreachable,
        [ResultKind.ValidationFailed] = ConnectionStatus.Unreachable,
        [ResultKind.NotFound] = ConnectionStatus.Unreachable,
        [ResultKind.InfrastructureError] = ConnectionStatus.Unreachable
    };

    internal async Task<RfbTestOutcome> TestAsync(
        RfbTestTarget target, SecretBuffer password, CancellationToken cancellationToken)
    {
        var trace = new RfbTrace();
        trace.Write($"連線 {target.Ip}:{target.Port} …");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ConnectTimeout);

        using var client = new TcpClient();
        await client.ConnectAsync(target.Ip, target.Port, timeout.Token);
        trace.Write("TCP 連線已建立");

        await using var stream = client.GetStream();
        var authenticated = await RfbHandshake.AuthenticateAsync(stream, password, trace, cancellationToken);

        return authenticated.Values
            .Select(result => new RfbTestOutcome(
                StatusBySecurityResult.GetValueOrDefault(result.SecurityResultCode, ConnectionStatus.WrongPassword),
                MessageBySecurityResult.GetValueOrDefault(result.SecurityResultCode, "驗證失敗"),
                trace.Text))
            .DefaultIfEmpty(new RfbTestOutcome(
                StatusByFailureKind.GetValueOrDefault(authenticated.Kind, ConnectionStatus.Unreachable),
                authenticated.Message,
                trace.Text))
            .First();
    }
}
