using System.Security.Cryptography;

namespace Vaeron.SharedKernel;

/// <summary>敏感值記憶體內載體，Dispose 時歸零底層位元組</summary>
public sealed class SecretBuffer : IDisposable
{
    private readonly byte[] _bytes;
    private bool _disposed;

    public SecretBuffer(byte[] bytes) => _bytes = bytes;

    public static SecretBuffer FromUtf8(string plainText) => new(System.Text.Encoding.UTF8.GetBytes(plainText));

    public int Length => _bytes.Length;

    public ReadOnlySpan<byte> AsSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _bytes;
    }

    public void Dispose()
    {
        if (_disposed) return;
        CryptographicOperations.ZeroMemory(_bytes);
        _disposed = true;
    }
}
