using System.Globalization;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Vaeron.Kernel;
using Vaeron.Kernel.Secrets;

namespace Vaeron.VNCSCP.Internal.Rfb;

/// <summary>交握完成後的安全結果。0 代表通過，其餘由呼叫端各自解讀。</summary>
public sealed record RfbAuthResult(uint SecurityResultCode);

/// <summary>RFB 版本交換、安全類型協商、VNC Authentication 挑戰回應。</summary>
public static class RfbHandshake
{
    private const byte VncAuthentication = 2;
    private const int LegacyMinorVersion = 3;

    /// <summary>協定次版本決定安全類型的協商形式。未列出的版本以清單形式協商。</summary>
    private static readonly Dictionary<int, Func<NetworkStream, RfbTrace, CancellationToken, Task<Result<byte>>>> NegotiatorByMinor = new()
    {
        [3] = NegotiateLegacyAsync,
        [4] = NegotiateLegacyAsync,
        [5] = NegotiateLegacyAsync,
        [6] = NegotiateLegacyAsync,
        [7] = NegotiateListAsync,
        [8] = NegotiateListAsync
    };

    public static async Task<Result<RfbAuthResult>> AuthenticateAsync(
        NetworkStream stream, SecretBuffer password, RfbTrace trace, CancellationToken cancellationToken)
    {
        var minor = await ExchangeVersionAsync(stream, trace, cancellationToken);
        var negotiate = NegotiatorByMinor.GetValueOrDefault(minor, NegotiateListAsync);
        var negotiated = await negotiate(stream, trace, cancellationToken);

        var challenged = await Task.WhenAll(
            negotiated.Values.Select(_ => RespondAsync(stream, password, trace, cancellationToken)));

        return challenged
            .Select(Result<RfbAuthResult>.Ok)
            .DefaultIfEmpty(Result<RfbAuthResult>.Fail(negotiated.Kind, negotiated.Message))
            .First();
    }

    private static async Task<int> ExchangeVersionAsync(
        NetworkStream stream, RfbTrace trace, CancellationToken cancellationToken)
    {
        var banner = await RfbStream.ReadExactAsync(stream, 12, cancellationToken);
        await RfbStream.WriteAsync(stream, banner, cancellationToken);

        var text = Encoding.ASCII.GetString(banner).TrimEnd('\n', '\r');
        trace.Write($"伺服器版本橫幅：{text}");
        trace.Write($"用戶端回應版本：{text}");

        return text.Split('.')
            .Skip(1)
            .Where(part => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            .Select(part => int.Parse(part, NumberStyles.Integer, CultureInfo.InvariantCulture))
            .DefaultIfEmpty(LegacyMinorVersion)
            .First();
    }

    private static async Task<Result<byte>> NegotiateListAsync(
        NetworkStream stream, RfbTrace trace, CancellationToken cancellationToken)
    {
        var count = (await RfbStream.ReadExactAsync(stream, 1, cancellationToken))[0];
        trace.Write($"security types 數量：{count}");

        var offered = await RfbStream.ReadExactAsync(stream, count, cancellationToken);
        trace.Write($"security types 清單：{string.Join(",", offered)}");

        var chosen = Result<byte>
            .When(offered.Length > 0, VncAuthentication, ResultKind.Conflict, "連線被拒絕（伺服器交握前即拒絕）")
            .Bind(type => Result<byte>.When(
                offered.Contains(VncAuthentication), type, ResultKind.Unavailable, "伺服器不支援 VNC Authentication"));

        foreach (var type in chosen.Values)
        {
            await RfbStream.WriteAsync(stream, [type], cancellationToken);
            trace.Write("選用 VNC Authentication(2)");
        }

        return chosen;
    }

    private static async Task<Result<byte>> NegotiateLegacyAsync(
        NetworkStream stream, RfbTrace trace, CancellationToken cancellationToken)
    {
        var announced = await RfbStream.ReadExactAsync(stream, 4, cancellationToken);
        var type = announced[3];
        trace.Write($"security type（舊式）：{type}");

        return Result<byte>
            .When(type > 0, type, ResultKind.Conflict, "連線被拒絕（伺服器交握前即拒絕）")
            .Bind(value => Result<byte>.When(
                value == VncAuthentication, value, ResultKind.Unavailable, "伺服器要求的驗證類型不是 VNC Authentication"));
    }

    private static async Task<RfbAuthResult> RespondAsync(
        NetworkStream stream, SecretBuffer password, RfbTrace trace, CancellationToken cancellationToken)
    {
        var challenge = await RfbStream.ReadExactAsync(stream, 16, cancellationToken);
        trace.Write("收到 16-byte challenge");

        await RfbStream.WriteAsync(stream, ComputeResponse(challenge, password), cancellationToken);
        trace.Write("送出 DES challenge-response");

        var code = RfbStream.BigEndian(await RfbStream.ReadExactAsync(stream, 4, cancellationToken));
        trace.Write($"security result：{code}");
        return new RfbAuthResult(code);
    }

    /// <summary>VNC Authentication 的 DES 金鑰：密碼截斷補零到 8 bytes，每個 byte 位元反轉。</summary>
    private static byte[] ComputeResponse(byte[] challenge, SecretBuffer password)
        => password.Use(secret =>
        {
            var key = secret.ToArray()
                .Concat(new byte[8])
                .Take(8)
                .Select(ReverseBits)
                .ToArray();

#pragma warning disable SYSLIB0021
            using var des = DES.Create();
            des.Mode = CipherMode.ECB;
            des.Padding = PaddingMode.None;
            des.Key = key;

            using var encryptor = des.CreateEncryptor();
            var response = new byte[16];
            encryptor.TransformBlock(challenge, 0, 16, response, 0);
            return response;
#pragma warning restore SYSLIB0021
        });

    private static byte ReverseBits(byte value)
        => (byte)Enumerable.Range(0, 8)
            .Aggregate(0, (accumulated, bit) => (accumulated << 1) | ((value >> bit) & 1));
}
