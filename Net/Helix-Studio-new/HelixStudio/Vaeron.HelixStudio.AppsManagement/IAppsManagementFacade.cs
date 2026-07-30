using Vaeron.Kernel;

namespace Vaeron.HelixStudio.AppsManagement;

/// <summary>供 AppLibrary（安裝）、UsbVoltageControl／RemoteBatchTransfer（服務執行中判斷）、
/// Shell（狀態列執行中數量）呼叫的唯一入口。</summary>
public interface IAppsManagementFacade
{
    public event Action? ToolsChanged;

    public Task<IReadOnlyList<InstalledToolSnapshot>> LoadInstalledToolListAsync(int workspaceId);

    public Task<Result> InstallToolAsync(InstallToolRequest request);

    public Task<Result> StartToolAsync(int id);

    public Task<Result> StopToolAsync(int id);

    public Task<Result> RemoveToolAsync(int id);

    public Task<Result> UpdateToolSettingsAsync(UpdateToolSettingsRequest request);

    public Task<Result> UnlockFieldsAsync(UnlockFieldsRequest request);

    /// <summary>供 UsbVoltageControl／RemoteBatchTransfer 判斷對應服務是否執行中（依名稱比對）。</summary>
    public Task<bool> IsToolRunningByNameAsync(IsToolRunningQuery query);

    public Task ApplyDeleteToolsByWorkspaceAsync(int workspaceId);

    /// <summary>App 關閉時清理所有仍在執行的子行程，由 App 根專案的 OnExit 呼叫</summary>
    public void ApplyDisposeAllRunningProcesses();
}
