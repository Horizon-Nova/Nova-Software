using Vaeron.Kernel;

namespace Vaeron.HelixStudio.EventLog.Internal;

internal sealed class EventLogFacade(EventLogService service) : IEventLogFacade
{
    public event Action? EventsChanged
    {
        add => service.Changed += value;
        remove => service.Changed -= value;
    }

    public Task<Result> RecordEventAsync(RecordEventRequest request)
        => service.RecordEventAsync(request);

    public Task<int> LoadUnreadCountAsync(int workspaceId)
        => service.LoadUnreadCountAsync(workspaceId);

    public Task<Result> MarkAsReadAsync(int workspaceId)
        => service.MarkAsReadAsync(workspaceId);

    public Task<LogSettingsSnapshot> LoadLogSettingsAsync()
        => service.LoadLogSettingsAsync();

    public Task<Result> UpdateLogSettingsAsync(LogSettingsSnapshot settings)
        => service.UpdateLogSettingsAsync(settings);

    public Task<IReadOnlyList<EventEntrySnapshot>> LoadEventListAsync(int workspaceId)
        => service.LoadEventListAsync(workspaceId);

    public Task ApplyDeleteEventsByWorkspaceAsync(int workspaceId)
        => service.DeleteEventsByWorkspaceAsync(workspaceId);

    public Task ApplyRetentionPolicyAsync()
        => service.ApplyRetentionPolicyAsync();
}
