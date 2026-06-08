using ExternalModules.DeploymentTool.App.Runtime;
using ExternalModules.DeploymentTool.Models.Deployment;
using ExternalModules.DeploymentTool.ViewModels.Base;

namespace ExternalModules.DeploymentTool.ViewModels;

/// <summary>
/// DeploymentTool 主頁的 UI 狀態機，協調畫布業務邏輯。
/// </summary>
internal sealed class DeploymentToolPageViewModel : ViewModelBase
{
    private readonly DeploymentToolRuntime _runtime = new();

    /// <summary>
    /// 依選項鍵建立畫布項目的業務資料。
    /// </summary>
    /// <param name="optionKey">使用者選取的服務選項鍵。</param>
    /// <returns>畫布項目的資料模型。</returns>
    public DeploymentCanvasItem BuildCanvasItem(string optionKey) =>
        _runtime.BuildCanvasItem(optionKey);

    /// <summary>
    /// 依待套用變更數量取得橫幅文字。
    /// </summary>
    /// <param name="pendingChangeCount">目前畫布上的待套用變更數量。</param>
    /// <returns>橫幅顯示文字；若無待套用變更則回傳空字串。</returns>
    public string QueryPendingChangesText(int pendingChangeCount) =>
        _runtime.QueryPendingChangesText(pendingChangeCount);
}
