using Vaeron.VNCSCP.Internal.Contracts;

namespace Vaeron.VNCSCP.Dashboard;

/// <summary>主畫面一列的組合快照：連線本身、該台的部署狀態、該台的近期事件。</summary>
public sealed record DashboardRowSnapshot
{
    public required ConnectionSnapshot Connection { get; init; }
    public required string DeployStateText { get; init; }
    public required string DeployMessage { get; init; }
    public required bool CanRetryDeploy { get; init; }
    public string? LastDeployTaskId { get; init; }
    public required IReadOnlyList<EventSnapshot> RecentEvents { get; init; }
}

