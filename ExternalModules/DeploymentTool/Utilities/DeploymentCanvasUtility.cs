using ExternalModules.DeploymentTool.Core;

namespace ExternalModules.DeploymentTool.Utilities;

public sealed class DeploymentCanvasUtility
{
    public DeploymentCanvasItem BuildCanvasItem(string optionKey)
    {
        var normalizedOptionKey = string.IsNullOrWhiteSpace(optionKey)
            ? "unknown"
            : optionKey.Trim();

        return normalizedOptionKey switch
        {
            "Project Folder" => new DeploymentCanvasItem
            {
                Title = "Project Folder",
                Subtitle = "Service will be created",
                StatusText = "待確認部署",
                BadgeText = "New",
                IsPending = true
            },
            "Database" => new DeploymentCanvasItem
            {
                Title = "Database",
                Subtitle = "Service will be created",
                StatusText = "待確認部署",
                BadgeText = "New",
                IsPending = true
            },
            "Docker Image" => new DeploymentCanvasItem
            {
                Title = "Docker Image",
                Subtitle = "Service will be created",
                StatusText = "待確認部署",
                BadgeText = "New",
                IsPending = true
            },
            "Volume" => new DeploymentCanvasItem
            {
                Title = "Volume",
                Subtitle = "Service will be created",
                StatusText = "待確認部署",
                BadgeText = "New",
                IsPending = true
            },
            "Test" => new DeploymentCanvasItem
            {
                Title = "Test",
                Subtitle = "Service will be created",
                StatusText = "待確認部署",
                BadgeText = "New",
                IsPending = true
            },
            _ => new DeploymentCanvasItem
            {
                Title = normalizedOptionKey,
                Subtitle = "Service will be created",
                StatusText = "待確認部署",
                BadgeText = "New",
                IsPending = true
            }
        };
    }

    public string QueryPendingChangesText(int pendingChangeCount) =>
        pendingChangeCount switch
        {
            <= 0 => string.Empty,
            1 => "Apply 1 change",
            _ => $"Apply {pendingChangeCount} changes"
        };
}
