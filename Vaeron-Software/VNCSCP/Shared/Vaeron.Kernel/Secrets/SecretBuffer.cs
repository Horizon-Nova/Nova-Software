using System.Security.Cryptography;

namespace Vaeron.Kernel.Secrets;

/// <summary>敏感值的承載體。</summary>
/// <remarks>
/// 承諾範圍：本執行個體持有的那一份位元組會在釋放時被歸零。
/// 不承諾：敏感值不存在於記憶體其他位置。回收行程移動物件時會複製,
/// 舊位置不會被歸零;任何轉為字串的動作會產生無法清除的副本。
/// 本型別縮短曝光窗口,不消除曝光。
///
/// 歸零使用 CryptographicOperations.ZeroMemory 而非陣列清除,
/// 後者可能被最佳化為無用寫入而移除。
/// </remarks>
public sealed class SecretBuffer : IDisposable
{
    private readonly byte[] _bytes;
    private readonly Lock _gate = new();
    private bool _disposed;

    /// <summary>接管傳入的陣列。呼叫端交出所有權,不得再持有該參考。</summary>
    public SecretBuffer(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        _bytes = bytes;
    }

    public int Length => _bytes.Length;

    /// <summary>借出唯讀視窗。不外洩內部陣列參考,呼叫端無法在委派結束後繼續存取。已釋放後呼叫為程式錯誤,擲出例外而非回傳失敗值。</summary>
    public void Use(Action<ReadOnlySpan<byte>> reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            reader(_bytes);
        }
    }

    /// <summary>借出唯讀視窗並回傳結果。</summary>
    public TResult Use<TResult>(SpanReader<TResult> reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return reader(_bytes);
        }
    }

    /// <summary>冪等。重複呼叫不擲出。</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            CryptographicOperations.ZeroMemory(_bytes);
            _disposed = true;
        }
    }
}

/// <summary>唯讀視窗的取用委派。ReadOnlySpan 無法作為泛型型別引數,故需具名委派。</summary>
public delegate TResult SpanReader<out TResult>(ReadOnlySpan<byte> value);
