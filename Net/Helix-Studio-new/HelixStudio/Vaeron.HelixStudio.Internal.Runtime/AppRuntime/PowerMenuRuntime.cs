namespace Vaeron.HelixStudio.Internal.Runtime.AppRuntime;

/// <summary>闔上筆電螢幕（LidCloseAction＝「顯示關閉選單」）時彈出的電源選單，觸發點見 MainWindow 的
/// GUID_LIDSWITCH_STATE_CHANGE 攔截；只管「要不要跳選單、使用者選了什麼」，不管選完之後系統動作怎麼執行——
/// 那是呼叫方（App 訂閱者）的事，同 ConfirmationRuntime／NotificationRuntime 的分工。</summary>
public enum PowerMenuAction
{
    None,
    Sleep,
    Hibernate,
    Shutdown,
}

public sealed class PowerMenuRuntime
{
    public event Func<Task<PowerMenuAction>>? PowerMenuRequested;

    /// <summary>回傳使用者選擇的動作，取消或沒有訂閱者時回傳 None——
    /// 安全預設值是「不執行任何動作」，理由同 ConfirmationRuntime 的無訂閱者判準。</summary>
    public async Task<PowerMenuAction> ShowAsync()
    {
        if (PowerMenuRequested is null) return PowerMenuAction.None;
        return await PowerMenuRequested.Invoke();
    }
}
