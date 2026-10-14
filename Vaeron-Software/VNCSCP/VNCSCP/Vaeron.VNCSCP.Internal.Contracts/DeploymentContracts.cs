using Vaeron.Kernel;

namespace Vaeron.VNCSCP.Internal.Contracts;

/// <summary>單一機台的部署狀態。</summary>
public enum DeployItemState
{
    /// <summary>等待指定時間抵達，尚未進入執行佇列。</summary>
    Scheduled,
    Pending,
    Running,
    Success,
    Failed,
    Cancelled
}

/// <summary>單一機台的進度更新。呼叫方收到後只更新對應那一列。</summary>
public sealed record DeployItemProgress
{
    public required string TaskId { get; init; }
    public required int ConnectionId { get; init; }
    public required DeployItemState State { get; init; }
    public required string StateText { get; init; }
    public required string Message { get; init; }
}

/// <summary>部署歷史的唯讀快照。顯示文字取自 View。</summary>
public sealed record DeploymentHistorySnapshot
{
    public required int Id { get; init; }
    public required DateTimeOffset DeployedAt { get; init; }
    public required string DeployedDisplay { get; init; }
    public required string TargetName { get; init; }
    public required string TargetIp { get; init; }
    public required string LocalPath { get; init; }
    public required string RemotePath { get; init; }
    public required bool Success { get; init; }
    public required string SuccessText { get; init; }
    public required string Message { get; init; }
    public required string OperatedBy { get; init; }
}

/// <summary>提交一次批次部署。</summary>
public sealed record DeploySubmission
{
    public required IReadOnlyList<int> SelectedTargetIds { get; init; }
    public required string LocalSourcePath { get; init; }
    public required string RemoteTargetPath { get; init; }

    /// <summary>null 表示立即派發，非 null 表示排程於此時間派發。</summary>
    public DateTimeOffset? ScheduledAt { get; init; }
}

public sealed record DeployTargetSnapshot
{
    public required int ConnectionId { get; init; }
    public required string ConnectionName { get; init; }
}

/// <summary>提交當下的任務目標快照，供呼叫方建立自己的任務彙總畫面狀態。任務不持久化。</summary>
public sealed record DeploySubmissionResult
{
    public required string TaskId { get; init; }
    public required string Label { get; init; }
    public required IReadOnlyList<DeployTargetSnapshot> Targets { get; init; }
}

/// <summary>重試單一機台，沿用原任務識別碼與原路徑。</summary>
public sealed record RetryItemRequest
{
    public required string TaskId { get; init; }
    public required int ConnectionId { get; init; }
    public required string LocalSourcePath { get; init; }
    public required string RemoteTargetPath { get; init; }
}

/// <summary>提交表單記住的上次路徑，供下次進畫面預填。</summary>
public sealed record DeployPreferencesSnapshot
{
    public string LastLocalSourcePath { get; init; } = "";
    public string LastRemoteTargetPath { get; init; } = "";
}

/// <summary>批次部署對外的唯一入口。</summary>
public interface IRemoteDeploymentFacade
{
    /// <summary>單一機台的進度變化。</summary>
    public event Action<DeployItemProgress>? ItemChanged;

    public ValueTask<Result<IReadOnlyList<DeploymentHistorySnapshot>>> LoadHistoryListAsync(CancellationToken cancellationToken);

    public ValueTask<Result<DeployPreferencesSnapshot>> LoadPreferencesAsync(CancellationToken cancellationToken);

    /// <summary>驗證、記住本次路徑，然後排入執行佇列或排程等待。驗證失敗不產生任務。</summary>
    public ValueTask<Result<DeploySubmissionResult>> SubmitDeploymentAsync(
        DeploySubmission submission, CancellationToken cancellationToken);

    /// <summary>原地重跑該台，不建立新任務。任務已被整批取消時回傳失敗。</summary>
    public Result RetryItem(RetryItemRequest request);

    /// <summary>取消該任務下尚未完成的機台。</summary>
    public void CancelTask(string taskId);

    /// <summary>該連線最近一次進度回報；沒有回報過時序列為空。</summary>
    public IReadOnlyList<DeployItemProgress> LatestProgressOf(int connectionId);
}
