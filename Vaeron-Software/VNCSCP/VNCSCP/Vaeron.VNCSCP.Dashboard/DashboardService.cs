using Vaeron.Kernel;
using Vaeron.Kernel.Secrets;
using Vaeron.VNCSCP.Internal.Contracts;

namespace Vaeron.VNCSCP.Dashboard;

/// <summary>不持有自己的資料，把三個功能的對外入口組合成一份逐列快照。</summary>
public sealed class DashboardService : IDisposable
{
    private const string NeverDeployedText = "尚未部署";

    private readonly IConnectionManagementFacade _connections;
    private readonly IRemoteDeploymentFacade _deployment;
    private readonly IEventLogFacade _eventLog;

    public DashboardService(
        IConnectionManagementFacade connections,
        IRemoteDeploymentFacade deployment,
        IEventLogFacade eventLog)
    {
        _connections = connections;
        _deployment = deployment;
        _eventLog = eventLog;
        _connections.Changed += OnConnectionsChanged;
        _deployment.ItemChanged += OnDeployItemChanged;
    }

    /// <summary>連線資料本身異動，整批重查的觸發點。</summary>
    internal event Action? Changed;

    /// <summary>單台部署進度變化，訂閱端只更新那一列。</summary>
    internal event Action<DeployItemProgress>? DeployProgressChanged;

    public void Dispose()
    {
        _connections.Changed -= OnConnectionsChanged;
        _deployment.ItemChanged -= OnDeployItemChanged;
    }

    #region 統一的查詢方法

    /// <summary>連線清單是整頁顯示的先決條件；事件查詢失敗只讓自己那一格空白。</summary>
    internal async ValueTask<Result<IReadOnlyList<DashboardRowSnapshot>>> LoadRowListAsync(
        ConnectionFilter filter, CancellationToken cancellationToken)
    {
        var connections = await _connections.LoadConnectionListAsync(filter, cancellationToken);
        var rows = new List<DashboardRowSnapshot>();

        foreach (var list in connections.Values)
        {
            var events = await _eventLog.LoadRecentEventListAsync(
                list.Select(item => item.Id).ToList(), cancellationToken);

            var eventsByConnection = events.Values
                .SelectMany(all => all)
                .GroupBy(item => item.RelatedConnectionId!.Value)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<EventSnapshot>)group.ToList());

            rows.AddRange(list.Select(item => Compose(item, eventsByConnection)));
        }

        return connections.Map(_ => (IReadOnlyList<DashboardRowSnapshot>)rows);
    }

    internal ValueTask<Result<IReadOnlyList<string>>> LoadGroupNameListAsync(CancellationToken cancellationToken)
        => _connections.LoadGroupNameListAsync(cancellationToken);

    /// <summary>單一連線的近期事件，部署進入終態時只重整那一格。</summary>
    internal ValueTask<Result<IReadOnlyList<EventSnapshot>>> LoadRecentEventListAsync(
        int connectionId, CancellationToken cancellationToken)
        => _eventLog.LoadRecentEventListAsync([connectionId], cancellationToken);

    internal ValueTask<Result<DeployPreferencesSnapshot>> LoadDeployPreferencesAsync(CancellationToken cancellationToken)
        => _deployment.LoadPreferencesAsync(cancellationToken);

    internal ValueTask<Result<string>> RevealPasswordAsync(int id, CancellationToken cancellationToken)
        => _connections.RevealPasswordAsync(id, cancellationToken);

    #endregion

    #region 基本 CRUD 操作

    internal ValueTask<Result> CreateConnectionAsync(
        CreateConnectionRequest request, SecretBuffer password, CancellationToken cancellationToken)
        => _connections.CreateConnectionAsync(request, password, cancellationToken);

    internal ValueTask<Result> UpdateConnectionAsync(
        UpdateConnectionRequest request, SecretBuffer? password, CancellationToken cancellationToken)
        => _connections.UpdateConnectionAsync(request, password, cancellationToken);

    internal ValueTask<Result> DeleteConnectionAsync(int id, CancellationToken cancellationToken)
        => _connections.DeleteConnectionAsync(id, cancellationToken);

    internal ValueTask<Result<ConnectionSnapshot>> TestConnectionAsync(int id, CancellationToken cancellationToken)
        => _connections.TestConnectionAsync(id, cancellationToken);

    internal ValueTask<Result> RenameGroupAsync(RenameGroupRequest request, CancellationToken cancellationToken)
        => _connections.RenameGroupAsync(request, cancellationToken);

    internal ValueTask<Result> UngroupAsync(string name, CancellationToken cancellationToken)
        => _connections.UngroupAsync(name, cancellationToken);

    internal ValueTask<Result<DeploySubmissionResult>> SubmitDeploymentAsync(
        DeploySubmission request, CancellationToken cancellationToken)
        => _deployment.SubmitDeploymentAsync(request, cancellationToken);

    internal Result RetryDeployItem(RetryItemRequest request) => _deployment.RetryItem(request);

    internal void CancelDeployTask(string taskId) => _deployment.CancelTask(taskId);

    #endregion

    private void OnConnectionsChanged() => Changed?.Invoke();

    private void OnDeployItemChanged(DeployItemProgress progress) => DeployProgressChanged?.Invoke(progress);

    private DashboardRowSnapshot Compose(
        ConnectionSnapshot connection, Dictionary<int, IReadOnlyList<EventSnapshot>> eventsByConnection)
    {
        var progress = _deployment.LatestProgressOf(connection.Id);

        return new DashboardRowSnapshot
        {
            Connection = connection,
            DeployStateText = progress.Select(item => item.StateText).DefaultIfEmpty(NeverDeployedText).First(),
            DeployMessage = progress.Select(item => item.Message).DefaultIfEmpty("").First(),
            CanRetryDeploy = progress.Any(item => item.State == DeployItemState.Failed),
            LastDeployTaskId = progress.Select(item => item.TaskId).FirstOrDefault(),
            RecentEvents = eventsByConnection.GetValueOrDefault(connection.Id, [])
        };
    }
}

