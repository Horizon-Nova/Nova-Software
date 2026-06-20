namespace ExternalModules.DeploymentTool.Models.Deployment;

public sealed class DeploymentEnvironmentItem
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public bool IsSelected { get; set; }
}

public sealed class DeploymentStatItem
{
    public string Label { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string AccentText { get; init; } = string.Empty;
}

public sealed class DeploymentServiceItem
{
    public string Name { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public string Runtime { get; init; } = string.Empty;

    public string PortText { get; init; } = string.Empty;

    public string StatusText { get; init; } = string.Empty;

    public string LastDeploymentText { get; init; } = string.Empty;
}

public sealed class DeploymentVolumeItem
{
    public string Name { get; init; } = string.Empty;

    public string MountPath { get; init; } = string.Empty;

    public string CapacityText { get; init; } = string.Empty;
}

public sealed class DeploymentEventItem
{
    public string TimeText { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string DetailText { get; init; } = string.Empty;

    public bool IsFailure { get; init; }
}

public sealed class DeploymentProjectSnapshot
{
    public string ProjectName { get; init; } = string.Empty;

    public string ProjectDescription { get; init; } = string.Empty;

    public string VirtualMachineName { get; init; } = string.Empty;

    public string TemplateName { get; init; } = string.Empty;

    public string ResourceText { get; init; } = string.Empty;

    public string DockerModeText { get; init; } = string.Empty;

    public string HostAddressText { get; init; } = string.Empty;

    public IReadOnlyList<DeploymentStatItem> Stats { get; init; } = [];

    public IReadOnlyList<DeploymentServiceItem> Services { get; init; } = [];

    public IReadOnlyList<DeploymentVolumeItem> Volumes { get; init; } = [];

    public IReadOnlyList<DeploymentEventItem> Events { get; init; } = [];
}

public sealed class DeploymentCanvasItem
{
    public string Title { get; init; } = string.Empty;

    public string Subtitle { get; init; } = string.Empty;

    public string StatusText { get; init; } = string.Empty;

    public string BadgeText { get; init; } = string.Empty;

    public bool IsPending { get; init; }
}
