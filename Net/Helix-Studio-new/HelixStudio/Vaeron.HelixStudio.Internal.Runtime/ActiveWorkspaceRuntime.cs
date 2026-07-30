namespace Vaeron.HelixStudio.Internal.Runtime;

/// <summary>作用中工作區 id 的唯讀快取，由 WorkspaceManagementService 在每次查詢／切換時回填。
/// 存在的唯一理由：EventLogAuditSink 需要在攔截當下同步取得作用中工作區 id 才能歸屬事件，
/// 但它不能呼叫 IWorkspaceManagementFacade——那個介面本身也被 FacadeAuditProxy 包了一層，
/// 呼叫它會反過來觸發一次新的攔截，形成「記錄一筆事件之前要先記錄一筆查詢工作區的事件」的無窮遞迴。
/// 這裡繞過 Facade／Proxy 兩層,直接讀寫這個內部快取。</summary>
public sealed class ActiveWorkspaceRuntime
{
    public int? CurrentId { get; private set; }

    public void SetCurrent(int? id) => CurrentId = id;
}
