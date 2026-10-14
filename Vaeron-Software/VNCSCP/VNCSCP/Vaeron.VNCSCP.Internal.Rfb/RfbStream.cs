using System.Net.Sockets;

namespace Vaeron.VNCSCP.Internal.Rfb;

/// <summary>RFB 位元組讀寫。讀不滿長度時由平台擲出，不在此處判定。</summary>
public static class RfbStream
{
    public static async Task WriteAsync(NetworkStream stream, byte[] payload, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task<byte[]> ReadExactAsync(NetworkStream stream, int count, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        await stream.ReadExactlyAsync(buffer, cancellationToken);
        return buffer;
    }

    public static async Task SkipAsync(NetworkStream stream, long count, CancellationToken cancellationToken)
    {
        var buffer = new byte[65536];
        var remaining = count;

        while (remaining > 0)
        {
            var take = (int)Math.Min(remaining, buffer.Length);
            await stream.ReadExactlyAsync(buffer.AsMemory(0, take), cancellationToken);
            remaining -= take;
        }
    }

    /// <summary>大端序四位元組轉數值。</summary>
    public static uint BigEndian(byte[] bytes) => bytes.Aggregate(0u, (accumulated, part) => (accumulated << 8) | part);

    /// <summary>數值轉大端序四位元組。</summary>
    public static byte[] ToBigEndian(uint value) =>
    [
        (byte)(value >> 24),
        (byte)(value >> 16),
        (byte)(value >> 8),
        (byte)value
    ];
}
