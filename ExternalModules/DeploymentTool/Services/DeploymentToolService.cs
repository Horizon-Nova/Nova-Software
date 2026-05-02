using ExternalModules.DeploymentTool.Core;
using ExternalModules.DeploymentTool.Utilities;

namespace ExternalModules.DeploymentTool.Services;

public sealed class DeploymentToolService
{
    private readonly DeploymentToolDashboardUtility _dashboardUtility = new();
    private readonly DeploymentCanvasUtility _deploymentCanvasUtility = new();

    public IReadOnlyList<DeploymentEnvironmentItem> QueryEnvironmentItems(string selectedEnvironmentKey) =>
        _dashboardUtility.QueryEnvironmentItems(selectedEnvironmentKey);

    public DeploymentProjectSnapshot QueryProjectSnapshot(string selectedEnvironmentKey) =>
        _dashboardUtility.QueryProjectSnapshot(selectedEnvironmentKey);

    public DeploymentCanvasItem BuildCanvasItem(string optionKey) =>
        _deploymentCanvasUtility.BuildCanvasItem(optionKey);

    public string QueryPendingChangesText(int pendingChangeCount) =>
        _deploymentCanvasUtility.QueryPendingChangesText(pendingChangeCount);
}
