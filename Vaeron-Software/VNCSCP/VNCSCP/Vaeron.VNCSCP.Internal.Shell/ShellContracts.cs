namespace Vaeron.VNCSCP.Internal.Shell;

/// <summary>一次操作的成敗與訊息，供狀態列顯示。</summary>
public sealed record OperationOutcome(bool Success, string Message);

/// <summary>顯示確認對話框並等待回應。同時間只存在一個確認框。</summary>
public interface IConfirmationService
{
    public ValueTask<bool> AskAsync(string message);
}

/// <summary>更新狀態列。只顯示最後一筆結果。</summary>
public interface INotificationService
{
    public void ShowResult(OperationOutcome outcome);
}

/// <summary>開啟或聚焦一個輔助功能的次視窗。呼叫方只認字串鍵，鍵與畫面型別的對映表在根組合層。</summary>
public interface INavigator
{
    public void NavigateTo(string moduleKey);
}
