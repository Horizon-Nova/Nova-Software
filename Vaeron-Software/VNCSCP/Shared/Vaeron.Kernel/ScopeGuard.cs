namespace Vaeron.Kernel;

/// <summary>離開範圍時必定執行的收尾動作。</summary>
/// <remarks>
/// 補的是平台空缺：using 只能綁在實作 IDisposable 的既有型別上,收尾動作本身不是資源時無處可掛。
/// 交出去的委派以 Interlocked 取走,重複釋放只會執行一次。
/// </remarks>
public sealed class ScopeGuard(Action onDispose) : IDisposable
{
    private Action? _onDispose = onDispose;

    public void Dispose() => Interlocked.Exchange(ref _onDispose, null)?.Invoke();
}
