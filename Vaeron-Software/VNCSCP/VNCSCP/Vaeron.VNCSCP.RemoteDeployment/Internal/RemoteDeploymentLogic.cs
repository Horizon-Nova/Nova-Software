using System.IO;
using Vaeron.Kernel;
using Vaeron.Kernel.Data.Models;
using Vaeron.VNCSCP.Internal.Contracts;

namespace Vaeron.VNCSCP.RemoteDeployment.Internal;

/// <summary>提交當下驗證完成的資料，交給佇列排程。</summary>
internal sealed record PreparedSubmission(
    string TaskId,
    string Label,
    IReadOnlyList<DeployTargetSnapshot> Targets,
    IReadOnlyList<int> ConnectionIds,
    string LocalSourcePath,
    string RemoteTargetPath,
    DateTimeOffset? ScheduledAt);

/// <summary>單台部署一次嘗試的結果。</summary>
internal sealed record DeployAttempt(DeployItemState State, string Message, string Diagnostic);

/// <summary>部署的業務規則集中處：提交驗證、單台部署與重試、歷史寫入。</summary>
internal sealed class RemoteDeploymentLogic(
    RemoteDeploymentStore store,
    DeployPreferencesStore preferencesStore,
    RfbFileTransferClient transferClient,
    IDeployTargetProvider targetProvider,
    IEventRecorder eventRecorder)
{
    private const string EventSource = "任務部署";
    private const int MaxAttempts = 3;
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CancelPollInterval = TimeSpan.FromMilliseconds(200);

    internal async Task<IReadOnlyList<DeploymentHistorySnapshot>> LoadHistoryListAsync(CancellationToken cancellationToken)
    {
        var rows = await store.QueryHistoryListAsync(cancellationToken);
        return rows.Select(ToSnapshot).ToList();
    }

    internal Task<DeployPreferencesSnapshot> LoadPreferencesAsync(CancellationToken cancellationToken)
        => preferencesStore.QueryPreferencesAsync(cancellationToken);

    /// <summary>驗證、記住本次路徑、解析目標名單。不碰佇列。</summary>
    internal async Task<Result<PreparedSubmission>> PrepareSubmissionAsync(
        DeploySubmission request, CancellationToken cancellationToken)
    {
        var validated = Result
            .When(request.SelectedTargetIds.Count > 0, ResultKind.ValidationFailed, "請先勾選至少一台部署目標")
            .Bind(() => Result.When(
                request.LocalSourcePath.Trim().Length > 0 && request.RemoteTargetPath.Trim().Length > 0,
                ResultKind.ValidationFailed, "請先填寫來源與遠端路徑"))
            .Bind(() => Result.When(
                ScheduleIsFuture(request), ResultKind.ValidationFailed, "排程時間必須晚於現在"));

        foreach (var _ in validated.Successes)
        {
            await preferencesStore.WritePreferencesAsync(new DeployPreferencesSnapshot
            {
                LastLocalSourcePath = request.LocalSourcePath,
                LastRemoteTargetPath = request.RemoteTargetPath
            }, cancellationToken);
        }

        var candidates = await validated.BindAsync(() => targetProvider.LoadTargetListAsync(cancellationToken));
        return candidates.Map(all => Compose(request, all));
    }

    private static bool ScheduleIsFuture(DeploySubmission request)
        => new[] { request.ScheduledAt }
            .OfType<DateTimeOffset>()
            .All(moment => moment > DateTimeOffset.UtcNow);

    private static PreparedSubmission Compose(DeploySubmission request, IReadOnlyList<DeployTargetCandidate> all)
    {
        var targets = all
            .Where(candidate => request.SelectedTargetIds.Contains(candidate.ConnectionId))
            .Select(candidate => new DeployTargetSnapshot
            {
                ConnectionId = candidate.ConnectionId,
                ConnectionName = candidate.Name
            })
            .ToList();

        var sourceName = Path.GetFileName(request.LocalSourcePath.TrimEnd('\\', '/'));

        return new PreparedSubmission(
            Guid.NewGuid().ToString("N"),
            $"{targets.Count} 台 · {sourceName}",
            targets,
            request.SelectedTargetIds,
            request.LocalSourcePath,
            request.RemoteTargetPath,
            request.ScheduledAt);
    }

    /// <summary>部署單一機台：自動重試含退避，每次狀態變化即回報。單台失敗不影響其他台。</summary>
    internal async Task DeployTargetAsync(
        DeployWorkItem work, Action<DeployAttempt> report, CancellationToken cancellationToken)
    {
        var located = await LocateTargetAsync(work.ConnectionId, cancellationToken);

        foreach (var failure in located.Failures)
        {
            report(new DeployAttempt(DeployItemState.Failed, failure, ""));
        }

        foreach (var target in located.Values)
        {
            await RunAttemptsAsync(work, target, report, cancellationToken);
        }
    }

    private async Task<Result<DeployTargetCandidate>> LocateTargetAsync(int connectionId, CancellationToken cancellationToken)
    {
        var candidates = await targetProvider.LoadTargetListAsync(cancellationToken);
        return candidates.Bind(all => Result<DeployTargetCandidate>.FromFirst(
            all.Where(candidate => candidate.ConnectionId == connectionId),
            ResultKind.NotFound,
            "找不到連線"));
    }

    private async Task RunAttemptsAsync(
        DeployWorkItem work, DeployTargetCandidate target, Action<DeployAttempt> report, CancellationToken cancellationToken)
    {
        var finished = new List<DeployAttempt>();
        var attempt = 0;

        while (finished.Count == 0 && attempt < MaxAttempts)
        {
            attempt++;
            var cancelled = new[] { attempt }
                .Where(_ => cancellationToken.IsCancellationRequested)
                .Select(_ => new DeployAttempt(DeployItemState.Cancelled, "已取消", ""))
                .ToList();

            finished.AddRange(cancelled);
            var running = cancelled.Count == 0;

            foreach (var _ in new[] { attempt }.Where(_ => running))
            {
                report(new DeployAttempt(DeployItemState.Running, RunningMessage(attempt), ""));
                finished.AddRange(await SendOnceAsync(work, target, attempt, cancellationToken));
            }

            foreach (var _ in new[] { attempt }.Where(_ => running && finished.Count == 0 && attempt < MaxAttempts))
            {
                await DelayWithCancelPollingAsync(TimeSpan.FromSeconds(2 * attempt), cancellationToken);
            }
        }

        var outcome = finished
            .DefaultIfEmpty(new DeployAttempt(DeployItemState.Failed, $"重試 {MaxAttempts} 次仍失敗", ""))
            .First();

        report(outcome);
        await WriteHistoryAsync(work, target, outcome, cancellationToken);
    }

    private static string RunningMessage(int attempt)
        => new[] { $"重試中（{attempt}/{MaxAttempts}）" }
            .Where(_ => attempt > 1)
            .DefaultIfEmpty("部署中…")
            .First();

    /// <summary>送一次。成功或被取消為終局，其餘留給下一次重試。</summary>
    private async Task<IReadOnlyList<DeployAttempt>> SendOnceAsync(
        DeployWorkItem work, DeployTargetCandidate target, int attempt, CancellationToken cancellationToken)
    {
        var secret = await targetProvider.ResolveSecretAsync(target.ConnectionId, cancellationToken);

        var terminal = secret.Failures
            .Select(message => new DeployAttempt(DeployItemState.Failed, message, ""))
            .ToList();

        foreach (var password in secret.Values)
        {
            using var owned = password;
            var request = new RfbTransferRequest(
                target.Ip, target.Port, work.LocalSourcePath, work.RemoteTargetPath, OperationTimeout);

            var sent = await Attempt.RunAsync(
                () => transferClient.SendAsync(request, owned, cancellationToken),
                ResultKind.Unavailable,
                "傳輸失敗：");

            await RecordAttemptAsync(target, attempt, sent, cancellationToken);
            terminal.AddRange(TerminalOf(sent, cancellationToken));
        }

        return terminal;
    }

    /// <summary>每一次真正送出的嘗試各記一則事件，成敗與交握診斷都留在事件明細裡。</summary>
    private async Task RecordAttemptAsync(
        DeployTargetCandidate target, int attempt, Result<RfbTransferOutcome> sent, CancellationToken cancellationToken)
    {
        var succeeded = sent.Values.Any(outcome => outcome.Success);
        var message = sent.Values.Select(outcome => outcome.Message).DefaultIfEmpty(sent.Message).First();
        var detail = sent.Values.Select(outcome => outcome.Diagnostic).DefaultIfEmpty("").First();

        await eventRecorder.RecordAsync(new RecordEventRequest
        {
            Level = new[] { EventLevel.Info }.Where(_ => succeeded).DefaultIfEmpty(EventLevel.Warning).First(),
            Source = EventSource,
            Message = $"{target.Name}：第 {attempt} 次 {message}",
            Detail = detail,
            RelatedConnectionId = target.ConnectionId
        }, cancellationToken);
    }

    private static IReadOnlyList<DeployAttempt> TerminalOf(Result<RfbTransferOutcome> sent, CancellationToken cancellationToken)
    {
        var cancelled = new[] { sent }
            .Where(_ => cancellationToken.IsCancellationRequested)
            .Select(_ => new DeployAttempt(DeployItemState.Cancelled, "已取消", ""))
            .ToList();

        var succeeded = sent.Values
            .Where(outcome => outcome.Success)
            .Select(outcome => new DeployAttempt(DeployItemState.Success, outcome.Message, outcome.Diagnostic))
            .ToList();

        return cancelled.Concat(succeeded).ToList();
    }

    /// <summary>退避等待採輪詢，取消最長只需等一個輪詢間隔即生效。</summary>
    private static async Task DelayWithCancelPollingAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        var elapsed = TimeSpan.Zero;

        while (elapsed < duration && cancellationToken.IsCancellationRequested is false)
        {
            await Task.Delay(CancelPollInterval, CancellationToken.None);
            elapsed += CancelPollInterval;
        }
    }

    /// <summary>歷史寫入失敗不得阻斷部署本身，結果已透過回報送出。</summary>
    private async Task WriteHistoryAsync(
        DeployWorkItem work, DeployTargetCandidate target, DeployAttempt outcome, CancellationToken cancellationToken)
        => await Attempt.RunAsync(
            () => store.InsertHistoryAsync(new DeploymentHistoryEntity
            {
                DeployedTicks = DateTimeOffset.UtcNow.UtcTicks,
                ConnectionId = target.ConnectionId,
                TargetName = target.Name,
                TargetIp = $"{target.Ip}:{target.Port}",
                LocalPath = work.LocalSourcePath,
                RemotePath = work.RemoteTargetPath,
                Success = outcome.State == DeployItemState.Success,
                Message = outcome.Message,
                OperatedBy = Environment.UserName
            }, cancellationToken),
            ResultKind.Unavailable,
            "寫入部署歷史失敗：");

    private static DeploymentHistorySnapshot ToSnapshot(DeploymentHistoryRow row) => new()
    {
        Id = row.Id,
        DeployedAt = new DateTimeOffset(row.DeployedTicks, TimeSpan.Zero),
        DeployedDisplay = row.DeployedDisplay,
        TargetName = row.TargetName,
        TargetIp = row.TargetIp,
        LocalPath = row.LocalPath,
        RemotePath = row.RemotePath,
        Success = row.Success,
        SuccessText = row.SuccessText,
        Message = row.Message,
        OperatedBy = row.OperatedBy
    };
}
