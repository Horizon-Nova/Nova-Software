namespace Vaeron.SharedKernel;

/// <summary>(bool success, string message) 寫入結果協定輔助</summary>
public static class ResultContract
{
    public const string FailPrefix = "[失敗] ";

    public static (bool success, string message) Ok(string message) => (true, message);

    public static (bool success, string message) Fail(string reason) => (false, FailPrefix + reason);
}
