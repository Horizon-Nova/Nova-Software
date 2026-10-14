using Vaeron.Kernel;
using Vaeron.VNCSCP.Internal.Contracts;

namespace Vaeron.VNCSCP.EventLog.Internal;

/// <summary>事件查詢與記錄的成敗。同時作為事件記錄的提供方。</summary>
internal sealed class EventLogService(EventLogLogic logic) : IEventRecorder
{
    /// <summary>任一筆事件被記錄後觸發。</summary>
    internal event Action? Changed;

    #region 統一的查詢方法

    internal ValueTask<Result<IReadOnlyList<EventSnapshot>>> LoadEventListAsync(
        EventFilter filter, CancellationToken cancellationToken)
        => Attempt.RunAsync(
            () => logic.LoadEventListAsync(filter, cancellationToken),
            ResultKind.Unavailable,
            "讀取事件失敗：");

    internal ValueTask<Result<IReadOnlyList<EventSnapshot>>> LoadRecentEventListAsync(
        IReadOnlyList<int> connectionIds, CancellationToken cancellationToken)
        => Attempt.RunAsync(
            () => logic.LoadRecentEventListAsync(connectionIds, cancellationToken),
            ResultKind.Unavailable,
            "讀取近期事件失敗：");

    #endregion

    #region 基本 CRUD 操作

    public async ValueTask<Result> RecordAsync(RecordEventRequest request, CancellationToken cancellationToken)
    {
        var recorded = await Attempt.RunAsync(
            () => logic.InsertEventAsync(request, cancellationToken),
            ResultKind.Unavailable,
            "記錄事件失敗：");

        foreach (var _ in recorded.Successes)
        {
            Changed?.Invoke();
        }

        return recorded;
    }

    internal ValueTask<Result> ExportAsync(EventFilter filter, string path, CancellationToken cancellationToken)
        => new(logic.ExportAsync(filter, path, cancellationToken));

    #endregion
}
