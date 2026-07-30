using Vaeron.Kernel;

namespace Vaeron.HelixStudio.AppsManagement.Internal;

internal sealed class AppsManagementFacade(AppsManagementService service) : IAppsManagementFacade
{
    public event Action? ToolsChanged
    {
        add => service.Changed += value;
        remove => service.Changed -= value;
    }

    public Task<IReadOnlyList<InstalledToolSnapshot>> LoadInstalledToolListAsync(int workspaceId)
        => service.LoadInstalledToolListAsync(workspaceId);

    public Task<Result> InstallToolAsync(InstallToolRequest request)
        => service.InstallToolAsync(request);

    public Task<Result> StartToolAsync(int id)
        => service.StartToolAsync(id);

    public Task<Result> StopToolAsync(int id)
        => service.StopToolAsync(id);

    public Task<Result> RemoveToolAsync(int id)
        => service.RemoveToolAsync(id);

    public Task<Result> UpdateToolSettingsAsync(UpdateToolSettingsRequest request)
        => service.UpdateToolSettingsAsync(request);

    public Task<Result> UnlockFieldsAsync(UnlockFieldsRequest request)
        => service.UnlockFieldsAsync(request);

    public Task<bool> IsToolRunningByNameAsync(IsToolRunningQuery query)
        => service.IsToolRunningByNameAsync(query);

    public Task ApplyDeleteToolsByWorkspaceAsync(int workspaceId)
        => service.DeleteToolsByWorkspaceAsync(workspaceId);

    public void ApplyDisposeAllRunningProcesses()
        => service.DisposeAllRunningProcesses();
}
