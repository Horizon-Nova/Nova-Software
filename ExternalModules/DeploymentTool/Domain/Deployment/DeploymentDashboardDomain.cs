using ExternalModules.DeploymentTool.Models.Deployment;

namespace ExternalModules.DeploymentTool.Domain.Deployment;

public sealed class DeploymentDashboardDomain
{
    public IReadOnlyList<DeploymentEnvironmentItem> QueryEnvironmentItems(string selectedEnvironmentKey)
    {
        var normalizedEnvironmentKey = string.IsNullOrWhiteSpace(selectedEnvironmentKey)
            ? "production"
            : selectedEnvironmentKey.Trim().ToLowerInvariant();

        return
        [
            BuildEnvironmentItem("production", "Production", normalizedEnvironmentKey),
            BuildEnvironmentItem("staging", "Staging", normalizedEnvironmentKey),
            BuildEnvironmentItem("test", "Test", normalizedEnvironmentKey)
        ];
    }

    public DeploymentProjectSnapshot QueryProjectSnapshot(string selectedEnvironmentKey)
    {
        var environmentKey = string.IsNullOrWhiteSpace(selectedEnvironmentKey)
            ? "production"
            : selectedEnvironmentKey.Trim().ToLowerInvariant();

        return new DeploymentProjectSnapshot
        {
            ProjectName = "Factory Portal",
            ProjectDescription = "Linux VM deployment snapshot for MVC, API, database, and cache services.",
            VirtualMachineName = $"factory-portal-{environmentKey}",
            TemplateName = "Ubuntu 24.04 + Docker Base",
            ResourceText = QueryResourceText(environmentKey),
            DockerModeText = "docker compose",
            HostAddressText = QueryHostAddressText(environmentKey),
            Stats =
            [
                new DeploymentStatItem
                {
                    Label = "Services",
                    Value = "4",
                    AccentText = "MVC / API / MySQL / Redis"
                },
                new DeploymentStatItem
                {
                    Label = "Last deployment",
                    Value = QueryLastDeploymentText(environmentKey),
                    AccentText = "Supports redeploy / rollback"
                },
                new DeploymentStatItem
                {
                    Label = "Validation status",
                    Value = environmentKey == "test" ? "Pending" : "Ready",
                    AccentText = environmentKey == "test" ? "Waiting for test environment provisioning" : "CPU / RAM profile validated"
                }
            ],
            Services = QueryServices(environmentKey),
            Volumes =
            [
                new DeploymentVolumeItem
                {
                    Name = "mysql-data",
                    MountPath = "/var/lib/mysql",
                    CapacityText = "80 GB"
                },
                new DeploymentVolumeItem
                {
                    Name = "portal-logs",
                    MountPath = "/app/logs",
                    CapacityText = "20 GB"
                },
                new DeploymentVolumeItem
                {
                    Name = "portal-uploads",
                    MountPath = "/app/uploads",
                    CapacityText = "120 GB"
                }
            ],
            Events = QueryEvents(environmentKey)
        };
    }

    private static DeploymentEnvironmentItem BuildEnvironmentItem(string key, string displayName, string selectedEnvironmentKey) =>
        new()
        {
            Key = key,
            DisplayName = displayName,
            IsSelected = string.Equals(key, selectedEnvironmentKey, StringComparison.OrdinalIgnoreCase)
        };

    private static string QueryResourceText(string environmentKey) =>
        environmentKey switch
        {
            "production" => "8 vCPU / 16 GB RAM / 160 GB SSD",
            "staging" => "4 vCPU / 8 GB RAM / 120 GB SSD",
            "test" => "2 vCPU / 4 GB RAM / 80 GB SSD",
            _ => "4 vCPU / 8 GB RAM / 120 GB SSD"
        };

    private static string QueryHostAddressText(string environmentKey) =>
        environmentKey switch
        {
            "production" => "10.10.8.15 / https://portal.company.local",
            "staging" => "10.10.8.25 / https://staging-portal.company.local",
            "test" => "Pending test environment IP",
            _ => "Pending host mapping"
        };

    private static string QueryLastDeploymentText(string environmentKey) =>
        environmentKey switch
        {
            "production" => "2026-04-15 14:32",
            "staging" => "2026-04-15 11:08",
            "test" => "Not deployed",
            _ => "Not deployed"
        };

    private static IReadOnlyList<DeploymentServiceItem> QueryServices(string environmentKey)
    {
        var webStatus = environmentKey == "test" ? "Pending" : "Healthy";
        var dbStatus = environmentKey == "production" ? "Healthy" : environmentKey == "staging" ? "Starting" : "Pending";

        return
        [
            new DeploymentServiceItem
            {
                Name = "portal-web",
                Type = "MVC",
                Runtime = "Dockerfile / ASP.NET Core 8",
                PortText = "80 -> 8080",
                StatusText = webStatus,
                LastDeploymentText = QueryLastDeploymentText(environmentKey)
            },
            new DeploymentServiceItem
            {
                Name = "portal-api",
                Type = "API",
                Runtime = "Dockerfile / ASP.NET Core 8",
                PortText = "81 -> 8081",
                StatusText = webStatus,
                LastDeploymentText = QueryLastDeploymentText(environmentKey)
            },
            new DeploymentServiceItem
            {
                Name = "mysql",
                Type = "Database",
                Runtime = "docker compose / mysql:8.4",
                PortText = "3306",
                StatusText = dbStatus,
                LastDeploymentText = QueryLastDeploymentText(environmentKey)
            },
            new DeploymentServiceItem
            {
                Name = "redis",
                Type = "Cache",
                Runtime = "docker compose / redis:7",
                PortText = "6379",
                StatusText = environmentKey == "test" ? "Pending" : "Healthy",
                LastDeploymentText = QueryLastDeploymentText(environmentKey)
            }
        ];
    }

    private static IReadOnlyList<DeploymentEventItem> QueryEvents(string environmentKey)
    {
        if (string.Equals(environmentKey, "test", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new DeploymentEventItem
                {
                    TimeText = "Not started",
                    Title = "Environment provisioning pending",
                    DetailText = "The Linux VM and Docker base image will be created after test environment approval."
                }
            ];
        }

        return
        [
            new DeploymentEventItem
            {
                TimeText = "14:32",
                Title = "Deployment finished",
                DetailText = "docker compose up -d completed and portal-web / portal-api reported Healthy."
            },
            new DeploymentEventItem
            {
                TimeText = "14:29",
                Title = "Configuration validation passed",
                DetailText = "compose, env, and volume settings passed pre-deployment checks."
            },
            new DeploymentEventItem
            {
                TimeText = "14:25",
                Title = "VM provisioning finished",
                DetailText = "Ubuntu 24.04 + Docker Base image provisioned with validated CPU and RAM profile."
            }
        ];
    }
}
