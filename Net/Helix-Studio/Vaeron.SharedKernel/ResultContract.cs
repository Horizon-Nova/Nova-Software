namespace Vaeron.SharedKernel;

/// <summary>Domain 寫入方法統一回傳 (bool success, string message) 的協定輔助——
/// 失敗訊息一律以 FailPrefix 開頭，讓 NotificationRuntime／呼叫端能單靠 success 布林值判斷樣式，
/// 不必反過來解析 message 字串（見 app-runtime.md／usage-patterns.md 的單一事實來源判準）。</summary>
public static class ResultContract
{
    public const string FailPrefix = "[失敗] ";

    public static (bool success, string message) Ok(string message) => (true, message);

    public static (bool success, string message) Fail(string reason) => (false, FailPrefix + reason);
}
