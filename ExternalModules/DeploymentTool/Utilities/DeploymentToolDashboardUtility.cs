using ExternalModules.DeploymentTool.Core;

namespace ExternalModules.DeploymentTool.Utilities;

public sealed class DeploymentToolDashboardUtility
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
            ProjectDescription = "以 Linux VM 模板承接 Docker 服務，集中管理 MVC、API、資料庫與掛載資料夾。",
            VirtualMachineName = $"factory-portal-{environmentKey}",
            TemplateName = "Ubuntu 24.04 + Docker Base",
            ResourceText = QueryResourceText(environmentKey),
            DockerModeText = "docker compose",
            HostAddressText = QueryHostAddressText(environmentKey),
            Stats =
            [
                new DeploymentStatItem
                {
                    Label = "服務數量",
                    Value = "4",
                    AccentText = "含 MVC / API / MySQL / Redis"
                },
                new DeploymentStatItem
                {
                    Label = "最近部署",
                    Value = QueryLastDeploymentText(environmentKey),
                    AccentText = "可做 redeploy / rollback"
                },
                new DeploymentStatItem
                {
                    Label = "虛擬機狀態",
                    Value = environmentKey == "test" ? "待建立" : "執行中",
                    AccentText = environmentKey == "test" ? "尚未從模板建立" : "已套用 CPU / RAM 設定"
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
            "test" => "尚未配置固定 IP",
            _ => "尚未配置"
        };

    private static string QueryLastDeploymentText(string environmentKey) =>
        environmentKey switch
        {
            "production" => "2026-04-15 14:32",
            "staging" => "2026-04-15 11:08",
            "test" => "尚未部署",
            _ => "尚未部署"
        };

    private static IReadOnlyList<DeploymentServiceItem> QueryServices(string environmentKey)
    {
        var webStatus = environmentKey == "test" ? "未建立" : "Healthy";
        var dbStatus = environmentKey == "production" ? "Healthy" : environmentKey == "staging" ? "Starting" : "未建立";

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
                StatusText = environmentKey == "test" ? "未建立" : "Healthy",
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
                    TimeText = "尚未啟動",
                    Title = "等待建立虛擬機",
                    DetailText = "先完成專案設定與模板選擇，再建立 Linux VM 並推送 Docker 檔案。"
                }
            ];
        }

        return
        [
            new DeploymentEventItem
            {
                TimeText = "14:32",
                Title = "部署完成",
                DetailText = "docker compose up -d 完成，portal-web 與 portal-api 回報 Healthy。"
            },
            new DeploymentEventItem
            {
                TimeText = "14:29",
                Title = "推送映像與環境變數",
                DetailText = "已將 compose、.env 與 volume 掛載設定同步到虛擬機。"
            },
            new DeploymentEventItem
            {
                TimeText = "14:25",
                Title = "建立虛擬機",
                DetailText = "從 Ubuntu 24.04 + Docker Base 模板複製新 VM，套用 CPU / RAM 設定。"
            }
        ];
    }
}
