using ExternalModules.DeploymentTool.Domain.Deployment;
using ExternalModules.DeploymentTool.Models.Deployment;

namespace ExternalModules.DeploymentTool.App.Runtime;

public sealed class DeploymentToolRuntime
{
    private readonly DeploymentDashboardDomain _dashboardDomain = new();
    private readonly DeploymentCanvasDomain _canvasDomain = new();

    public IReadOnlyList<DeploymentEnvironmentItem> QueryEnvironmentItems(string selectedEnvironmentKey) =>
        _dashboardDomain.QueryEnvironmentItems(selectedEnvironmentKey);

    public DeploymentProjectSnapshot QueryProjectSnapshot(string selectedEnvironmentKey) =>
        _dashboardDomain.QueryProjectSnapshot(selectedEnvironmentKey);

    public DeploymentCanvasItem BuildCanvasItem(string optionKey) =>
        _canvasDomain.BuildCanvasItem(optionKey);

    public string QueryPendingChangesText(int pendingChangeCount) =>
        _canvasDomain.QueryPendingChangesText(pendingChangeCount);
}
