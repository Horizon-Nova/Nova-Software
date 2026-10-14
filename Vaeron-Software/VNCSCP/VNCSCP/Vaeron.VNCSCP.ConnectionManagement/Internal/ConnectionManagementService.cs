using System.Globalization;
using Vaeron.Kernel;
using Vaeron.Kernel.Data.Models;
using Vaeron.Kernel.Secrets;
using Vaeron.VNCSCP.Internal.Contracts;

namespace Vaeron.VNCSCP.ConnectionManagement.Internal;

/// <summary>連線操作的成敗與事件記錄。同時作為部署目標的提供方。</summary>
internal sealed class ConnectionManagementService(
    ConnectionManagementLogic logic,
    IEventRecorder eventRecorder) : IDeployTargetProvider
{
    private const string EventSource = "連線管理";
    private const string TestEventSource = "連線測試";

    /// <summary>任何寫入完成後觸發。</summary>
    internal event Action? Changed;

    #region 統一的查詢方法

    internal async ValueTask<Result<IReadOnlyList<ConnectionSnapshot>>> LoadConnectionListAsync(
        ConnectionFilter filter, CancellationToken cancellationToken)
    {
        var rows = await logic.LoadConnectionListAsync(filter, cancellationToken);
        return Result<IReadOnlyList<ConnectionSnapshot>>.Ok(rows.Select(ToSnapshot).ToList());
    }

    internal async ValueTask<Result<IReadOnlyList<string>>> LoadGroupNameListAsync(CancellationToken cancellationToken)
        => Result<IReadOnlyList<string>>.Ok(await logic.LoadGroupNameListAsync(cancellationToken));

    public async ValueTask<Result<IReadOnlyList<DeployTargetCandidate>>> LoadTargetListAsync(CancellationToken cancellationToken)
    {
        var rows = await logic.LoadConnectionListAsync(new ConnectionFilter(), cancellationToken);
        return Result<IReadOnlyList<DeployTargetCandidate>>.Ok(rows.Select(ToCandidate).ToList());
    }

    public ValueTask<Result<SecretBuffer>> ResolveSecretAsync(int connectionId, CancellationToken cancellationToken)
        => new(logic.ResolveSecretAsync(connectionId, cancellationToken));

    #endregion

    #region 基本 CRUD 操作

    internal async ValueTask<Result> CreateConnectionAsync(
        CreateConnectionRequest request, SecretBuffer password, CancellationToken cancellationToken)
    {
        var created = await logic.CreateConnectionAsync(request, password, cancellationToken);

        foreach (var entity in created.Values)
        {
            await RecordAsync(EventLevel.Info, $"新增連線「{request.Name}」", DetailBlock(
                ("動作", "新增連線"),
                ("目標", $"{request.Ip}:{request.Port}"),
                ("名稱", request.Name),
                ("群組", request.GroupName ?? "未分組")), entity.Id, EventSource, cancellationToken);
            Changed?.Invoke();
        }

        return created.Discard();
    }

    internal async ValueTask<Result> UpdateConnectionAsync(
        UpdateConnectionRequest request, SecretBuffer? password, CancellationToken cancellationToken)
    {
        var updated = await logic.UpdateConnectionAsync(request, password, cancellationToken);
        var passwordChanged = new[] { "是" }.Where(_ => password is not null).DefaultIfEmpty("否").First();

        foreach (var _ in updated.Successes)
        {
            await RecordAsync(EventLevel.Info, $"更新連線「{request.Name}」", DetailBlock(
                ("動作", "更新連線"),
                ("連線編號", request.Id.ToString(CultureInfo.InvariantCulture)),
                ("目標", $"{request.Ip}:{request.Port}"),
                ("名稱", request.Name),
                ("群組", request.GroupName ?? "未分組"),
                ("密碼變更", passwordChanged)), request.Id, EventSource, cancellationToken);
            Changed?.Invoke();
        }

        return updated;
    }

    internal async ValueTask<Result> DeleteConnectionAsync(int id, CancellationToken cancellationToken)
    {
        var loaded = await logic.LoadConnectionAsync(id, cancellationToken);
        var deleted = Result.Fail(loaded.Kind, loaded.Message);

        foreach (var row in loaded.Values)
        {
            deleted = await logic.DeleteConnectionAsync(id, cancellationToken);

            foreach (var _ in deleted.Successes)
            {
                await RecordAsync(EventLevel.Warning, $"刪除連線「{row.Name}」", DetailBlock(
                    ("動作", "刪除連線"),
                    ("連線編號", id.ToString(CultureInfo.InvariantCulture)),
                    ("名稱", row.Name)), id, EventSource, cancellationToken);
                Changed?.Invoke();
            }
        }

        return deleted;
    }

    internal ValueTask<Result<string>> RevealPasswordAsync(int id, CancellationToken cancellationToken)
        => new(logic.RevealPasswordAsync(id, cancellationToken));

    internal async ValueTask<Result<ConnectionSnapshot>> TestConnectionAsync(int id, CancellationToken cancellationToken)
    {
        var tested = await logic.TestConnectionAsync(id, cancellationToken);

        foreach (var result in tested.Values)
        {
            await RecordAsync(
                LevelByStatus.GetValueOrDefault(result.Outcome.Status, EventLevel.Error),
                $"{result.Row.Name}：{result.Outcome.Message}",
                result.Outcome.Diagnostic,
                result.Row.Id,
                TestEventSource,
                cancellationToken);
            Changed?.Invoke();
        }

        return tested.Map(result => ToSnapshot(result.Row));
    }

    internal async ValueTask<Result> RenameGroupAsync(RenameGroupRequest request, CancellationToken cancellationToken)
    {
        var renamed = await logic.RenameGroupAsync(request, cancellationToken);

        foreach (var _ in renamed.Successes)
        {
            await RecordAsync(EventLevel.Info, $"群組「{request.OldName}」更名為「{request.NewName}」", DetailBlock(
                ("動作", "群組更名"),
                ("原名稱", request.OldName),
                ("新名稱", request.NewName)), null, EventSource, cancellationToken);
            Changed?.Invoke();
        }

        return renamed;
    }

    internal async ValueTask<Result> UngroupAsync(string name, CancellationToken cancellationToken)
    {
        var ungrouped = await logic.UngroupAsync(name, cancellationToken);

        foreach (var _ in ungrouped.Successes)
        {
            await RecordAsync(EventLevel.Warning, $"取消群組「{name}」", DetailBlock(
                ("動作", "取消群組"),
                ("名稱", name)), null, EventSource, cancellationToken);
            Changed?.Invoke();
        }

        return ungrouped;
    }

    #endregion

    private static readonly Dictionary<ConnectionStatus, EventLevel> LevelByStatus = new()
    {
        [ConnectionStatus.Unknown] = EventLevel.Info,
        [ConnectionStatus.Reachable] = EventLevel.Info,
        [ConnectionStatus.WrongPassword] = EventLevel.Error,
        [ConnectionStatus.Locked] = EventLevel.Warning,
        [ConnectionStatus.Unreachable] = EventLevel.Error
    };

    private static ConnectionSnapshot ToSnapshot(ConnectionRow row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        Ip = row.Ip,
        Port = row.Port,
        Status = (ConnectionStatus)row.Status,
        StatusText = row.StatusText,
        LastTestedAt = row.LastTestedAt,
        LastTestedDisplay = row.LastTestedDisplay,
        LastTestMessage = row.LastTestMessage,
        CreatedAt = row.CreatedAt,
        GroupName = row.GroupName,
        GroupDisplayName = row.GroupDisplayName
    };

    private static DeployTargetCandidate ToCandidate(ConnectionRow row) => new()
    {
        ConnectionId = row.Id,
        Name = row.Name,
        Ip = row.Ip,
        Port = row.Port,
        GroupName = row.GroupName
    };

    /// <summary>記錄失敗不得阻斷本類別的既有流程，故不理會回傳的結果。</summary>
    private async Task RecordAsync(
        EventLevel level, string message, string detail, int? relatedConnectionId, string source, CancellationToken cancellationToken)
        => await eventRecorder.RecordAsync(new RecordEventRequest
        {
            Level = level,
            Source = source,
            Message = message,
            Detail = detail,
            RelatedConnectionId = relatedConnectionId
        }, cancellationToken);

    private static string DetailBlock(params (string Key, string Value)[] fields)
        => string.Join("\n", fields.Select(field => $"{field.Key}：{field.Value}"));
}
