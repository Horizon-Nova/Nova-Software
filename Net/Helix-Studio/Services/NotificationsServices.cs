using Helix_Studio.Model.Notifications;

namespace Helix_Studio.Services;

/// <summary>主視窗殼層的橫幅通知廣播與通知匣（收件匣）存取。</summary>
public sealed class NotificationsServices
{
    const int MaxInboxItems = 100;

    readonly object _inboxLock = new();

    readonly List<ShellInboxItem> _inbox = new();

    /// <summary>當有即時通知應顯示於殼層（例如狀態列氣泡）時觸發。</summary>
    public event Action<ShellNotificationMessage>? NotificationPublished;

    /// <summary>當通知匣內容變更（新增、全已讀、清除）時觸發。</summary>
    public event Action? InboxChanged;

    /// <summary>發佈一則通知；若內文與標題皆空白則略過。會寫入通知匣上限內。</summary>
    public void Publish(ShellNotificationMessage notification)
    {
        if (string.IsNullOrWhiteSpace(notification.MessageText) &&
            string.IsNullOrWhiteSpace(notification.TitleText)) return;

        var item = new ShellInboxItem
        {
            Id          = Guid.NewGuid(),
            ReceivedAt  = DateTimeOffset.UtcNow,
            TitleText   = notification.TitleText,
            MessageText = notification.MessageText,
            Level       = notification.Level,
            IsRead      = false,
        };

        lock (_inboxLock)
        {
            _inbox.Insert(0, item);
            while (_inbox.Count > MaxInboxItems)
                _inbox.RemoveAt(_inbox.Count - 1);
        }

        NotificationPublished?.Invoke(notification);
        InboxChanged?.Invoke();
    }

    /// <summary>以最新在前排序的收件匣副本。</summary>
    public IReadOnlyList<ShellInboxItem> QueryInboxNewestFirst()
    {
        lock (_inboxLock)
            return _inbox.ToList();
    }

    /// <summary>未讀通知筆數。</summary>
    public int QueryUnreadCount()
    {
        lock (_inboxLock)
            return _inbox.Count(x => !x.IsRead);
    }

    /// <summary>將收件匣內所有項目標示為已讀並觸發 <see cref="InboxChanged"/>。</summary>
    public void MarkAllRead()
    {
        lock (_inboxLock)
        {
            foreach (var u in _inbox)
                u.IsRead = true;
        }

        InboxChanged?.Invoke();
    }

    /// <summary>清空通知匣並觸發 <see cref="InboxChanged"/>。</summary>
    public void ClearInbox()
    {
        lock (_inboxLock)
            _inbox.Clear();

        InboxChanged?.Invoke();
    }
}

