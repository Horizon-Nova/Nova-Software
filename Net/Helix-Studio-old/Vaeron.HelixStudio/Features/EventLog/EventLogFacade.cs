using Vaeron.HelixStudio.Features.EventLog.Contracts;

namespace Vaeron.HelixStudio.Features.EventLog;

/// <summary>供其他 Feature 寫事件、查未讀數/日誌設定的唯一入口。</summary>
public sealed class EventLogFacade(EventLogDomain domain)
{
    public event Action? EventsChanged
    {
        add => domain.Changed += value;
        remove => domain.Changed -= value;
    }

    public Task<(bool success, string message)> RecordEventAsync(RecordEventRequest request)
        => domain.RecordEventAsync(request);

    public Task<int> LoadUnreadCountAsync(int workspaceId)
        => domain.LoadUnreadCountAsync(workspaceId);

    public Task<(bool success, string message)> MarkAsReadAsync(int workspaceId)
        => domain.MarkAsReadAsync(workspaceId);

    public Task<LogSettingsSnapshot> LoadLogSettingsAsync()
        => domain.LoadLogSettingsAsync();

    public Task<(bool success, string message)> UpdateLogSettingsAsync(LogSettingsSnapshot settings)
        => domain.UpdateLogSettingsAsync(settings);

    public Task<IReadOnlyList<EventEntrySnapshot>> LoadEventListAsync(int workspaceId)
        => domain.LoadEventListAsync(workspaceId);

    public Task ApplyDeleteEventsByWorkspaceAsync(int workspaceId)
        => domain.DeleteEventsByWorkspaceAsync(workspaceId);

    public Task ApplyRetentionPolicyAsync()
        => domain.ApplyRetentionPolicyAsync();
}
