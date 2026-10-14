using Vaeron.Kernel;
using Vaeron.VNCSCP.Internal.Contracts;

namespace Vaeron.VNCSCP.EventLog.Internal;

/// <summary>只委派，不含邏輯。</summary>
internal sealed class EventLogFacade(EventLogService service) : IEventLogFacade
{
    public event Action? Changed
    {
        add => service.Changed += value;
        remove => service.Changed -= value;
    }

    public ValueTask<Result<IReadOnlyList<EventSnapshot>>> LoadEventListAsync(
        EventFilter filter, CancellationToken cancellationToken)
        => service.LoadEventListAsync(filter, cancellationToken);

    public ValueTask<Result<IReadOnlyList<EventSnapshot>>> LoadRecentEventListAsync(
        IReadOnlyList<int> connectionIds, CancellationToken cancellationToken)
        => service.LoadRecentEventListAsync(connectionIds, cancellationToken);

    public ValueTask<Result> ExportAsync(EventFilter filter, string path, CancellationToken cancellationToken)
        => service.ExportAsync(filter, path, cancellationToken);
}
