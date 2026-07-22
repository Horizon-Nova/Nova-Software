using Vaeron.HelixStudio.Features.EventLog;
using Vaeron.HelixStudio.Features.EventLog.Contracts;
using Vaeron.HelixStudio.Features.RemoteBatchTransfer.Contracts;
using Vaeron.HelixStudio.Features.RemoteBatchTransfer.Data;
using Vaeron.HelixStudio.Features.WorkspaceManagement;
using Vaeron.SharedKernel;

namespace Vaeron.HelixStudio.Features.RemoteBatchTransfer;

/// <summary>遠端批次傳檔業務邏輯</summary>
public sealed class RemoteBatchTransferDomain(
    RemoteBatchTransferRepository repo,
    RemoteTransferRepository transferRepo,
    LocalFileSystemRepository localFs,
    EventLogFacade eventLog,
    WorkspaceManagementFacade workspaceFacade)
{
    private readonly List<TransferTaskSnapshot> _sessionTasks = [];

    /// <summary>連線狀態／上次任務結果（僅本次執行期間，不落地）</summary>
    private readonly Dictionary<int, ConnectionStatus> _connectionStatus = [];
    private readonly Dictionary<int, (string message, DateTimeOffset time)> _lastResults = [];

    #region 事件中繼

    public event Action? Changed;

    #endregion

    #region 統一的查詢方法

    public async Task<IReadOnlyList<RemoteConnectionSnapshot>> LoadConnectionListAsync()
        => (await repo.QueryConnectionListAsync()).Select(ToSnapshot).ToList();

    public async Task<RemoteConnectionSnapshot?> LoadConnectionAsync(int id)
    {
        var connection = await repo.QueryConnectionAsync(id);
        return connection is null ? null : ToSnapshot(connection);
    }

    public IReadOnlyList<string> LoadDriveRoots() => localFs.ListDriveRoots();

    public IReadOnlyList<string> LoadSubdirectories(string path) => localFs.ListSubdirectories(path);

    public IReadOnlyList<TransferTaskSnapshot> LoadSessionTasks() => _sessionTasks;

    #endregion

    #region 基本 CRUD 操作

    public async Task<(bool success, string message)> CreateConnectionAsync(CreateConnectionRequest request)
    {
        await repo.InsertConnectionAsync(new remote_connection
        {
            name = request.Name,
            host = request.Host,
            port = request.Port,
            auth_method = request.AuthMethod.ToString(),
            account = request.Account,
        });
        Changed?.Invoke();
        return ResultContract.Ok("已新增連線");
    }

    public async Task<(bool success, string message)> DeleteConnectionAsync(int id)
    {
        var removed = await repo.DeleteConnectionAsync(id);
        if (!removed) return ResultContract.Fail("找不到此連線");
        Changed?.Invoke();
        return ResultContract.Ok("已刪除");
    }

    public async Task<(bool success, string message)> TestConnectionAsync(int id)
    {
        var connection = await repo.QueryConnectionAsync(id);
        if (connection is null) return ResultContract.Fail("找不到此連線");

        _connectionStatus[id] = ConnectionStatus.Testing;
        Changed?.Invoke();

        var (success, message) = await transferRepo.TestConnectionAsync(connection.host, connection.port);

        _connectionStatus[id] = success ? ConnectionStatus.Connected : ConnectionStatus.Disconnected;
        _lastResults[id] = (message, DateTimeOffset.UtcNow);
        await LogEventAsync(success ? EventSeverity.Info : EventSeverity.Warning, $"測試連線「{connection.name}」：{message}");
        Changed?.Invoke();

        return (success, message);
    }

    public async Task<(bool success, string message)> UpdateConnectionAsync(int id, CreateConnectionRequest request)
    {
        await repo.InsertConnectionAsync(new remote_connection
        {
            id = id,
            name = request.Name,
            host = request.Host,
            port = request.Port,
            auth_method = request.AuthMethod.ToString(),
            account = request.Account,
        });
        Changed?.Invoke();
        return ResultContract.Ok("已更新連線");
    }

    /// <summary>發布批次任務（允許部分派送，缺路徑的目標會被跳過）</summary>
    public async Task<TransferTaskSnapshot> DispatchTaskAsync(
        TransferAction action, string sourcePath, string defaultDestPath, IReadOnlyList<(int connectionId, string name, string? destOverride)> targets)
    {
        var connections = await repo.QueryConnectionListAsync();

        var results = new List<TransferTargetSnapshot>();
        foreach (var (connectionId, name, destOverride) in targets)
        {
            var destPath = destOverride ?? defaultDestPath;
            if (string.IsNullOrWhiteSpace(destPath))
            {
                results.Add(new TransferTargetSnapshot
                {
                    ConnectionId = connectionId,
                    Name = name,
                    DestPath = "",
                    Status = TransferTargetStatus.Cancelled,
                    Message = "沒有可解析的目的路徑，已跳過",
                });
                continue;
            }

            var connection = connections.FirstOrDefault(c => c.id == connectionId);
            var (success, message) = connection is null
                ? (false, "找不到此連線")
                : await transferRepo.TransferFileAsync(connection.host, connection.port, sourcePath, destPath, action == TransferAction.Move);

            results.Add(new TransferTargetSnapshot
            {
                ConnectionId = connectionId,
                Name = name,
                DestPath = destPath,
                Status = success ? TransferTargetStatus.Completed : TransferTargetStatus.Failed,
                Message = message,
            });
            _lastResults[connectionId] = (message, DateTimeOffset.UtcNow);

            await LogEventAsync(success ? EventSeverity.Info : EventSeverity.Warning,
                $"{(action == TransferAction.Copy ? "複製" : "移動")}到「{name}」：{message}");
        }

        var task = new TransferTaskSnapshot
        {
            Id = Guid.NewGuid(),
            Action = action,
            SourcePath = sourcePath,
            DefaultDestPath = defaultDestPath,
            DispatchedAt = DateTimeOffset.UtcNow,
            Targets = results,
        };
        _sessionTasks.Insert(0, task);
        Changed?.Invoke();
        return task;
    }

    #endregion

    private async Task LogEventAsync(EventSeverity severity, string message)
    {
        var active = await workspaceFacade.LoadActiveWorkspaceAsync();
        if (active is null) return;

        await eventLog.RecordEventAsync(new RecordEventRequest
        {
            WorkspaceId = active.Id,
            ToolName = "遠端批次傳檔",
            Severity = severity,
            Message = message,
        });
    }

    private RemoteConnectionSnapshot ToSnapshot(remote_connection x)
    {
        _lastResults.TryGetValue(x.id, out var lastResult);
        return new RemoteConnectionSnapshot
        {
            Id = x.id,
            Name = x.name,
            Host = x.host,
            Port = x.port,
            AuthMethod = Enum.Parse<Contracts.AuthMethod>(x.auth_method),
            Account = x.account,
            Status = _connectionStatus.GetValueOrDefault(x.id, ConnectionStatus.Disconnected),
            LastResultMessage = lastResult.message,
            LastResultTime = lastResult == default ? null : lastResult.time,
        };
    }
}
