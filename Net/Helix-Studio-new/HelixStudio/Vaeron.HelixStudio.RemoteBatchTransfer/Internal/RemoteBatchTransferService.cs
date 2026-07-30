using Vaeron.HelixStudio.EventLog;
using Vaeron.HelixStudio.RemoteBatchTransfer.Internal.Models;
using Vaeron.HelixStudio.WorkspaceManagement;
using Vaeron.Kernel;

namespace Vaeron.HelixStudio.RemoteBatchTransfer.Internal;

/// <summary>遠端批次傳檔業務邏輯</summary>
internal sealed class RemoteBatchTransferService(
    RemoteBatchTransferStore store,
    RemoteTransferStore transferStore,
    LocalFileSystemStore localFs,
    IEventLogFacade eventLog,
    IWorkspaceManagementFacade workspaceFacade)
{
    private readonly List<TransferTaskSnapshot> _sessionTasks = [];

    /// <summary>連線狀態／上次任務結果（僅本次執行期間，不落地）</summary>
    private readonly Dictionary<int, ConnectionStatus> _connectionStatus = [];
    private readonly Dictionary<int, (string message, DateTimeOffset time)> _lastResults = [];

    public event Action? Changed;

    public async Task<IReadOnlyList<RemoteConnectionSnapshot>> LoadConnectionListAsync()
        => (await store.QueryConnectionListAsync()).Select(ToSnapshot).ToList();

    public async Task<RemoteConnectionSnapshot?> LoadConnectionAsync(int id)
    {
        var connection = await store.QueryConnectionAsync(id);
        return connection is null ? null : ToSnapshot(connection);
    }

    public IReadOnlyList<string> LoadDriveRoots() => localFs.ListDriveRoots();

    public IReadOnlyList<string> LoadSubdirectories(string path) => localFs.ListSubdirectories(path);

    public IReadOnlyList<TransferTaskSnapshot> LoadSessionTasks() => _sessionTasks;

    public async Task<Result> CreateConnectionAsync(CreateConnectionRequest request)
    {
        await store.InsertConnectionAsync(new RemoteConnectionRow
        {
            name = request.Name,
            host = request.Host,
            port = request.Port,
            auth_method = request.AuthMethod.ToString(),
            account = request.Account,
        });
        Changed?.Invoke();
        return Ok("已新增連線");
    }

    public async Task<Result> DeleteConnectionAsync(int id)
    {
        var removed = await store.DeleteConnectionAsync(id);
        if (!removed) return Result.Fail(ResultKind.NotFound, "找不到此連線");
        Changed?.Invoke();
        return Ok("已刪除");
    }

    public async Task<Result> TestConnectionAsync(int id)
    {
        var connection = await store.QueryConnectionAsync(id);
        if (connection is null) return Result.Fail(ResultKind.NotFound, "找不到此連線");

        _connectionStatus[id] = ConnectionStatus.Testing;
        Changed?.Invoke();

        var tested = await transferStore.TestConnectionAsync(new TestConnectionRequest(connection.host, connection.port));

        _connectionStatus[id] = tested.IsSuccess ? ConnectionStatus.Connected : ConnectionStatus.Disconnected;
        _lastResults[id] = (tested.Message, DateTimeOffset.UtcNow);
        await LogEventAsync(tested.IsSuccess ? EventSeverity.Info : EventSeverity.Warning, $"測試連線「{connection.name}」：{tested.Message}");
        Changed?.Invoke();

        return tested;
    }

    public async Task<Result> UpdateConnectionAsync(UpdateConnectionRequest request)
    {
        await store.InsertConnectionAsync(new RemoteConnectionRow
        {
            id = request.Id,
            name = request.Data.Name,
            host = request.Data.Host,
            port = request.Data.Port,
            auth_method = request.Data.AuthMethod.ToString(),
            account = request.Data.Account,
        });
        Changed?.Invoke();
        return Ok("已更新連線");
    }

    /// <summary>發布批次任務（允許部分派送，缺路徑的目標會被跳過）</summary>
    public async Task<TransferTaskSnapshot> DispatchTaskAsync(DispatchTaskRequest request)
    {
        var connections = await store.QueryConnectionListAsync();

        var results = new List<TransferTargetSnapshot>();
        foreach (var target in request.Targets)
        {
            var destPath = target.DestOverride ?? request.DefaultDestPath;
            if (string.IsNullOrWhiteSpace(destPath))
            {
                results.Add(new TransferTargetSnapshot
                {
                    ConnectionId = target.ConnectionId,
                    Name = target.Name,
                    DestPath = "",
                    Status = TransferTargetStatus.Cancelled,
                    Message = "沒有可解析的目的路徑，已跳過",
                });
                continue;
            }

            var connection = connections.FirstOrDefault(c => c.id == target.ConnectionId);
            var transferred = connection is null
                ? Result.Fail(ResultKind.NotFound, "找不到此連線")
                : await transferStore.TransferFileAsync(new TransferFileRequest(
                    connection.host, connection.port, request.SourcePath, destPath, request.Action == TransferAction.Move));

            results.Add(new TransferTargetSnapshot
            {
                ConnectionId = target.ConnectionId,
                Name = target.Name,
                DestPath = destPath,
                Status = transferred.IsSuccess ? TransferTargetStatus.Completed : TransferTargetStatus.Failed,
                Message = transferred.Message,
            });
            _lastResults[target.ConnectionId] = (transferred.Message, DateTimeOffset.UtcNow);

            await LogEventAsync(transferred.IsSuccess ? EventSeverity.Info : EventSeverity.Warning,
                $"{(request.Action == TransferAction.Copy ? "複製" : "移動")}到「{target.Name}」：{transferred.Message}");
        }

        var task = new TransferTaskSnapshot
        {
            Id = Guid.NewGuid(),
            Action = request.Action,
            SourcePath = request.SourcePath,
            DefaultDestPath = request.DefaultDestPath,
            DispatchedAt = DateTimeOffset.UtcNow,
            Targets = results,
        };
        _sessionTasks.Insert(0, task);
        Changed?.Invoke();
        return task;
    }

    private static Result Ok(string message) => new() { Kind = ResultKind.Success, Message = message };

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

    private RemoteConnectionSnapshot ToSnapshot(RemoteConnectionRow x)
    {
        _lastResults.TryGetValue(x.id, out var lastResult);
        return new RemoteConnectionSnapshot
        {
            Id = x.id,
            Name = x.name,
            Host = x.host,
            Port = x.port,
            AuthMethod = Enum.Parse<AuthMethod>(x.auth_method),
            Account = x.account,
            Status = _connectionStatus.GetValueOrDefault(x.id, ConnectionStatus.Disconnected),
            LastResultMessage = lastResult.message,
            LastResultTime = lastResult == default ? null : lastResult.time,
        };
    }
}
