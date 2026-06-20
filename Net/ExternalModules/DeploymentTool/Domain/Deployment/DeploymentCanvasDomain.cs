using ExternalModules.DeploymentTool.Models.Deployment;

namespace ExternalModules.DeploymentTool.Domain.Deployment;

public sealed class DeploymentCanvasDomain
{
    public DeploymentCanvasItem BuildCanvasItem(string optionKey)
    {
        var normalizedOptionKey = string.IsNullOrWhiteSpace(optionKey) ? "unknown" : optionKey.Trim();
        return BuildPendingCanvasItem(normalizedOptionKey);
    }

    public string QueryPendingChangesText(int pendingChangeCount) =>
        pendingChangeCount switch
        {
            <= 0 => string.Empty,
            1 => "Apply 1 change",
            _ => $"Apply {pendingChangeCount} changes"
        };

    private static DeploymentCanvasItem BuildPendingCanvasItem(string title) =>
        new()
        {
            Title = title,
            Subtitle = "Service will be created",
            StatusText = "Pending",
            BadgeText = "New",
            IsPending = true
        };
}
