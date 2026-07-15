namespace Vaeron.Platform.AppRuntime;

/// <summary>畫面導覽事件——AppRuntime 只傳遞「想切到哪個模組」的請求，不決定這代表什麼業務意義。
/// 瀏覽歷史（上一頁／下一頁）由呼叫方（MainShellViewModel）自行維護 Stack，這裡只負責轉拋事件。</summary>
public sealed class NavigationRuntime
{
    public event Action<string>? NavigationRequested;

    public void ApplyNavigateToModule(string moduleKey)
        => NavigationRequested?.Invoke(moduleKey);
}
