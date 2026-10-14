using Vaeron.Kernel;

namespace Vaeron.VNCSCP.Internal.Contracts;

/// <summary>事件等級。等級文字由 Vw_EventLog 產生。</summary>
public enum EventLevel
{
    Info,
    Warning,
    Error
}

/// <summary>記錄一筆事件。來源字串由呼叫方自訂。</summary>
public sealed record RecordEventRequest
{
    public required EventLevel Level { get; init; }
    public required string Source { get; init; }
    public required string Message { get; init; }
    public string Detail { get; init; } = "";
    public int? RelatedConnectionId { get; init; }
}

/// <summary>對外提供記錄一筆事件的能力。實作在事件日誌，失敗不得阻斷呼叫方的流程。</summary>
public interface IEventRecorder
{
    public ValueTask<Result> RecordAsync(RecordEventRequest request, CancellationToken cancellationToken);
}

/// <summary>一筆事件的唯讀快照。</summary>
public sealed record EventSnapshot
{
    public required int Id { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required EventLevel Level { get; init; }
    public required string LevelText { get; init; }
    public required string OccurredDisplay { get; init; }
    public required string Source { get; init; }
    public required string Message { get; init; }
    public required string Detail { get; init; }
    public required bool HasDetail { get; init; }
    public int? RelatedConnectionId { get; init; }
}

/// <summary>事件查詢條件。三個值原樣送進 SQL，本端不解讀。</summary>
public sealed record EventFilter
{
    public string Search { get; init; } = "";

    /// <summary>時間範圍鍵：All／Today／Last7／Last30。</summary>
    public string TimeRange { get; init; } = "All";

    /// <summary>等級鍵：All／Info／Warning／Error。</summary>
    public string Level { get; init; } = "All";
}

/// <summary>事件日誌對外的唯一入口。</summary>
public interface IEventLogFacade
{
    /// <summary>任一筆事件被記錄後觸發，供已開啟的清單重新載入。</summary>
    public event Action? Changed;

    public ValueTask<Result<IReadOnlyList<EventSnapshot>>> LoadEventListAsync(
        EventFilter filter, CancellationToken cancellationToken);

    /// <summary>多筆連線各自的近期事件，筆數由 View 決定。</summary>
    public ValueTask<Result<IReadOnlyList<EventSnapshot>>> LoadRecentEventListAsync(
        IReadOnlyList<int> connectionIds, CancellationToken cancellationToken);

    public ValueTask<Result> ExportAsync(EventFilter filter, string path, CancellationToken cancellationToken);
}
