namespace Vaeron.HelixStudio.Internal.Runtime.AppRuntime;

public enum DataDensity
{
    Comfortable,
    Compact,
}

/// <summary>UiDensityRuntime 存在的理由：資料密度（列高）由 Settings 持久化,但 EventLog／AppsManagement
/// 等多個畫面都要讀它調整表格列高。若這些 Feature 直接依賴 SettingsFacade,Settings 反過來又要呈現
/// EventLog／AppsManagement 的畫面（工作區、日誌分類承載），會構成專案參考循環——故密度改用廣播,
/// Settings 是唯一寫入方,其餘 Feature 只讀,同 NavigationRuntime／NotificationRuntime 的分工。</summary>
public sealed class UiDensityRuntime
{
    public DataDensity Current { get; private set; } = DataDensity.Comfortable;

    public event Action? DensityChanged;

    public void ApplySetDensity(DataDensity density)
    {
        if (Current == density) return;
        Current = density;
        DensityChanged?.Invoke();
    }
}
