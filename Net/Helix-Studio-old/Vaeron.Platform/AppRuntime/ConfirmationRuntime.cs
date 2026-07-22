namespace Vaeron.Platform.AppRuntime;

public sealed class ConfirmationRuntime
{
    public event Func<string, Task<bool>>? ConfirmRequested;

    public async Task<bool> AskAsync(string message)
    {
        if (ConfirmRequested is null) return false; // 沒有訂閱者時，安全預設值是「不confirm」
        return await ConfirmRequested.Invoke(message);
    }
}
