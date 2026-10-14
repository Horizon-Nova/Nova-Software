using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vaeron.Kernel;
using Vaeron.Kernel.Data.Models;
using Vaeron.Kernel.Guarding;
using Vaeron.VNCSCP.Internal.Contracts;

namespace Vaeron.VNCSCP.EventLog.Internal;

/// <summary>事件的業務規則集中處：把資料列換成對外形狀，以及匯出成檔案。</summary>
internal sealed class EventLogLogic(EventLogStore store)
{
    private static readonly JsonSerializerOptions ExportOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    internal async Task<IReadOnlyList<EventSnapshot>> LoadEventListAsync(
        EventFilter filter, CancellationToken cancellationToken)
    {
        var rows = await store.QueryEventListAsync(filter, cancellationToken);
        return rows.Select(ToSnapshot).ToList();
    }

    internal async Task<IReadOnlyList<EventSnapshot>> LoadRecentEventListAsync(
        IReadOnlyList<int> connectionIds, CancellationToken cancellationToken)
    {
        var rows = await store.QueryRecentEventListAsync(connectionIds, cancellationToken);
        return rows.Select(ToSnapshot).ToList();
    }

    internal Task InsertEventAsync(RecordEventRequest request, CancellationToken cancellationToken)
        => store.InsertEventAsync(new EventLogEntity
        {
            OccurredUtcTicks = DateTimeOffset.UtcNow.UtcTicks,
            Level = (int)request.Level,
            Source = request.Source,
            Message = request.Message,
            Detail = request.Detail,
            RelatedConnectionId = request.RelatedConnectionId
        }, cancellationToken);

    /// <summary>依當前條件匯出 JSON。中文原樣輸出，等級輸出文字。</summary>
    internal async Task<Result> ExportAsync(EventFilter filter, string path, CancellationToken cancellationToken)
    {
        var events = await LoadEventListAsync(filter, cancellationToken);
        var json = JsonSerializer.Serialize(events, ExportOptions);
        var outcome = Result.Ok();

        await Guard.RunAsync(
            () => File.WriteAllTextAsync(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken),
            error => outcome = Result.Fail(ResultKind.Unavailable, $"匯出失敗：{error.Message}"));

        return outcome;
    }

    private static EventSnapshot ToSnapshot(EventLogRow row) => new()
    {
        Id = row.Id,
        OccurredAt = new DateTimeOffset(row.OccurredUtcTicks, TimeSpan.Zero),
        Level = (EventLevel)row.Level,
        LevelText = row.LevelText,
        OccurredDisplay = row.OccurredDisplay,
        Source = row.Source,
        Message = row.Message,
        Detail = row.Detail,
        HasDetail = row.HasDetail > 0,
        RelatedConnectionId = row.RelatedConnectionId
    };

    private static EventSnapshot ToSnapshot(ConnectionRecentEventRow row) => new()
    {
        Id = row.Id,
        OccurredAt = new DateTimeOffset(row.OccurredUtcTicks, TimeSpan.Zero),
        Level = (EventLevel)row.Level,
        LevelText = row.LevelText,
        OccurredDisplay = row.OccurredDisplay,
        Source = row.Source,
        Message = row.Message,
        Detail = row.Detail,
        HasDetail = row.HasDetail > 0,
        RelatedConnectionId = row.RelatedConnectionId
    };
}
