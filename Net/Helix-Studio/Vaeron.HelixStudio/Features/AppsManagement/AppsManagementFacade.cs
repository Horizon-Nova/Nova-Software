using Vaeron.HelixStudio.Features.AppsManagement.Contracts;

namespace Vaeron.HelixStudio.Features.AppsManagement;

/// <summary>供 AppLibrary（安裝）、UsbVoltageControl／RemoteBatchTransfer（服務執行中判斷）、
/// MainShell（狀態列執行中數量）呼叫的唯一入口。</summary>
public sealed class AppsManagementFacade(AppsManagementDomain domain)
{
    public event Action? ToolsChanged
    {
        add => domain.Changed += value;
        remove => domain.Changed -= value;
    }

    public Task<IReadOnlyList<InstalledToolSnapshot>> LoadInstalledToolListAsync(int workspaceId)
        => domain.LoadInstalledToolListAsync(workspaceId);

    public Task<(bool success, string message)> InstallToolAsync(InstallToolRequest request)
        => domain.InstallToolAsync(request);

    public Task<(bool success, string message)> StartToolAsync(int id)
        => domain.StartToolAsync(id);

    public Task<(bool success, string message)> StopToolAsync(int id)
        => domain.StopToolAsync(id);

    public Task<(bool success, string message)> RemoveToolAsync(int id)
        => domain.RemoveToolAsync(id);

    /// <summary>供 UsbVoltageControl／RemoteBatchTransfer 判斷對應服務是否執行中（依名稱比對）。</summary>
    public async Task<bool> IsToolRunningByNameAsync(int workspaceId, string toolName)
    {
        var list = await domain.LoadInstalledToolListAsync(workspaceId);
        return list.Any(t => t.Name == toolName && t.IsRunning);
    }

    public Task ApplyDeleteToolsByWorkspaceAsync(int workspaceId)
        => domain.DeleteToolsByWorkspaceAsync(workspaceId);
}
