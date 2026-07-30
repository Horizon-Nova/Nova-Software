using Vaeron.Kernel;

namespace Vaeron.HelixStudio.EventLog;

/// <summary>供其他 Feature 寫事件、查未讀數/日誌設定的唯一入口。</summary>
public interface IEventLogFacade
{
    public event Action? EventsChanged;

    public Task<Result> RecordEventAsync(RecordEventRequest request);

    public Task<int> LoadUnreadCountAsync(int workspaceId);

    public Task<Result> MarkAsReadAsync(int workspaceId);

    public Task<LogSettingsSnapshot> LoadLogSettingsAsync();

    public Task<Result> UpdateLogSettingsAsync(LogSettingsSnapshot settings);

    public Task<IReadOnlyList<EventEntrySnapshot>> LoadEventListAsync(int workspaceId);

    /// <summary>刪除工作區時連動清理事件記錄，由呼叫方（Settings 的工作區 CRUD）在刪除工作區成功後呼叫</summary>
    public Task ApplyDeleteEventsByWorkspaceAsync(int workspaceId);

    public Task ApplyRetentionPolicyAsync();
}
