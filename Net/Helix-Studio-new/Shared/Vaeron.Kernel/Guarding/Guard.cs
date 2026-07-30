namespace Vaeron.Kernel.Guarding;

/// <summary>
/// 無呼叫方情境下的例外出口。
///
/// 適用於框架簽名強制為 void 的位置：事件處理常式、計時器回呼、
/// 資源釋放、背景迴圈的單一工作。這些位置沒有呼叫方能接收 Result,
/// 例外若不在此攔截即逃逸至全域處理或終止程序。
///
/// 本型別不決定任何事：後續行為由呼叫端以失敗處置委派指定。
/// 失敗處置的契約是絕不擲出——它不受本型別保護,擲出將直接逸出。
/// </summary>
public static class Guard
{
    public static void Run(Action work, Action<Exception> onFailure)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            work();
        }
        catch (Exception ex)
        {
            onFailure(ex);
        }
    }

    public static async Task RunAsync(Func<Task> work, Action<Exception> onFailure)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            await work().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            onFailure(ex);
        }
    }

    public static async Task RunAsync(
        Func<CancellationToken, Task> work,
        Action<Exception> onFailure,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            await work(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 取消是正常終止,不是失敗
        }
        catch (Exception ex)
        {
            onFailure(ex);
        }
    }
}
