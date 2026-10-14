using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Vaeron.Kernel;
using Vaeron.VNCSCP.Internal.Contracts;

namespace Vaeron.VNCSCP.RemoteDeployment.Internal;

/// <summary>佇列裡的一件工作：一台機器、一份來源、一個遠端路徑。</summary>
internal sealed record DeployWorkItem(string TaskId, int ConnectionId, string LocalSourcePath, string RemoteTargetPath);

/// <summary>排程等待中的一件工作。時間抵達後才進入執行佇列。</summary>
internal sealed record ScheduledWorkItem(DeployWorkItem Work, DateTimeOffset ScheduledAt);

/// <summary>背景部署佇列：以單一機台為工作單位跨機台有界併發，支援連續提交、單台重試、排程派發。</summary>
internal sealed class RemoteDeploymentRunner(RemoteDeploymentLogic logic, IOptions<RemoteDeploymentOptions> options)
{
    private static readonly TimeSpan ScheduleTick = TimeSpan.FromSeconds(1);

    /// <summary>狀態文字由狀態值域決定，逐個對應。</summary>
    private static readonly Dictionary<DeployItemState, string> TextByState = new()
    {
        [DeployItemState.Scheduled] = "排程中",
        [DeployItemState.Pending] = "佇列中",
        [DeployItemState.Running] = "執行中",
        [DeployItemState.Success] = "成功",
        [DeployItemState.Failed] = "失敗",
        [DeployItemState.Cancelled] = "已取消"
    };

    private readonly Channel<DeployWorkItem> _channel = Channel.CreateUnbounded<DeployWorkItem>();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _taskCancellation = new();
    private readonly ConcurrentDictionary<int, DeployItemProgress> _latestByConnection = new();
    private readonly SemaphoreSlim _gate = new(options.Value.MaxParallelDeployments);
    private readonly List<ScheduledWorkItem> _scheduled = [];
    private readonly Lock _scheduledLock = new();
    private readonly Lock _consumerLock = new();
    private Task? _consumer;
    private Task? _scheduler;

    internal event Action<DeployItemProgress>? ItemChanged;

    internal static string TextOf(DeployItemState state) => TextByState.GetValueOrDefault(state, "");

    /// <summary>排程時間為空或已過即刻排入，未來時間先進排程等待區。</summary>
    internal void Submit(PreparedSubmission prepared)
    {
        _taskCancellation[prepared.TaskId] = new CancellationTokenSource();

        var works = prepared.ConnectionIds
            .Select(id => new DeployWorkItem(prepared.TaskId, id, prepared.LocalSourcePath, prepared.RemoteTargetPath))
            .ToList();

        var delayed = new[] { prepared.ScheduledAt }
            .OfType<DateTimeOffset>()
            .Where(moment => moment > DateTimeOffset.UtcNow)
            .ToList();

        foreach (var moment in delayed)
        {
            Schedule(works, moment);
        }

        foreach (var _ in new[] { prepared }.Where(_ => delayed.Count == 0))
        {
            works.ForEach(Enqueue);
        }
    }

    /// <summary>原地重排該台，不建立新任務。任務已被整批取消時拒絕。</summary>
    internal Result RetryItem(RetryItemRequest request)
    {
        var cancelled = _taskCancellation.GetValueOrDefault(request.TaskId)?.IsCancellationRequested is true;

        var admitted = Result.When(cancelled is false, ResultKind.Conflict, "此任務已被取消，無法重試");

        foreach (var _ in admitted.Successes)
        {
            Enqueue(new DeployWorkItem(
                request.TaskId, request.ConnectionId, request.LocalSourcePath, request.RemoteTargetPath));
        }

        return admitted;
    }

    /// <summary>取消整個任務：排程等待中與已在佇列或執行中的機台一併取消。</summary>
    internal void CancelTask(string taskId)
    {
        foreach (var source in new[] { _taskCancellation.GetValueOrDefault(taskId) }.OfType<CancellationTokenSource>())
        {
            source.Cancel();
        }

        var removed = new List<ScheduledWorkItem>();

        lock (_scheduledLock)
        {
            removed.AddRange(_scheduled.Where(item => item.Work.TaskId == taskId));
            _scheduled.RemoveAll(item => item.Work.TaskId == taskId);
        }

        foreach (var item in removed)
        {
            Report(item.Work, DeployItemState.Cancelled, "已取消");
        }
    }

    internal IReadOnlyList<DeployItemProgress> LatestProgressOf(int connectionId)
        => new[] { _latestByConnection.GetValueOrDefault(connectionId) }.OfType<DeployItemProgress>().ToList();

    private void Schedule(IReadOnlyList<DeployWorkItem> works, DateTimeOffset moment)
    {
        lock (_scheduledLock)
        {
            foreach (var work in works)
            {
                Report(work, DeployItemState.Scheduled, $"排程於 {moment.ToLocalTime():MM-dd HH:mm} 派發");
                _scheduled.Add(new ScheduledWorkItem(work, moment));
            }

            _scheduler = StillRunning(_scheduler) ?? Task.Run(RunSchedulerAsync);
        }
    }

    private void Enqueue(DeployWorkItem work)
    {
        Report(work, DeployItemState.Pending, "佇列中…");
        _channel.Writer.TryWrite(work);

        lock (_consumerLock)
        {
            _consumer = StillRunning(_consumer) ?? Task.Run(ConsumeAsync);
        }
    }

    private static Task? StillRunning(Task? task)
        => new[] { task }.OfType<Task>().Where(item => item.IsCompleted is false).FirstOrDefault();

    /// <summary>每秒掃一次排程清單，時間抵達的機台轉入執行佇列。</summary>
    private async Task RunSchedulerAsync()
    {
        using var timer = new PeriodicTimer(ScheduleTick);

        while (await timer.WaitForNextTickAsync())
        {
            var due = new List<ScheduledWorkItem>();

            lock (_scheduledLock)
            {
                var now = DateTimeOffset.UtcNow;
                due.AddRange(_scheduled.Where(item => item.ScheduledAt <= now));
                _scheduled.RemoveAll(item => item.ScheduledAt <= now);
            }

            foreach (var item in due)
            {
                Enqueue(item.Work);
            }
        }
    }

    private async Task ConsumeAsync()
    {
        while (await _channel.Reader.WaitToReadAsync())
        {
            while (_channel.Reader.TryRead(out var work))
            {
                await _gate.WaitAsync();
                _ = RunOneAsync(work);
            }
        }
    }

    private async Task RunOneAsync(DeployWorkItem work)
    {
        using var release = new ScopeGuard(() => _gate.Release());

        var token = _taskCancellation.GetValueOrDefault(work.TaskId)?.Token ?? CancellationToken.None;

        var cancelled = new[] { work }.Where(_ => token.IsCancellationRequested).ToList();

        foreach (var item in cancelled)
        {
            Report(item, DeployItemState.Cancelled, "已取消");
        }

        foreach (var item in new[] { work }.Where(_ => cancelled.Count == 0))
        {
            await logic.DeployTargetAsync(item, attempt => Report(item, attempt.State, attempt.Message), token);
        }
    }

    private void Report(DeployWorkItem work, DeployItemState state, string message)
    {
        var progress = new DeployItemProgress
        {
            TaskId = work.TaskId,
            ConnectionId = work.ConnectionId,
            State = state,
            StateText = TextOf(state),
            Message = message
        };

        _latestByConnection[work.ConnectionId] = progress;
        ItemChanged?.Invoke(progress);
    }
}
