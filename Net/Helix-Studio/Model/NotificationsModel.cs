using System.Globalization;

namespace Helix_Studio.Model.Notifications;

/// <summary>殼層推播或通知之嚴重程度。</summary>
public enum ShellNotificationLevel
{
    /// <summary>一般資訊。</summary>
    Information,

    /// <summary>成功。</summary>
    Success,

    /// <summary>警告。</summary>
    Warning,

    /// <summary>錯誤。</summary>
    Error
}

/// <summary>即時通知訊息內容（例如 Toast）。</summary>
public sealed class ShellNotificationMessage
{
    /// <summary>標題文字。</summary>
    public string TitleText { get; init; } = string.Empty;

    /// <summary>本文。</summary>
    public string MessageText { get; init; } = string.Empty;

    /// <summary>通知層級。</summary>
    public ShellNotificationLevel Level { get; init; } = ShellNotificationLevel.Information;

    /// <summary>顯示持續時間（毫秒）。</summary>
    public int DurationMilliseconds { get; init; } = 3200;
}

/// <summary>通知匣內單一則通知。</summary>
public sealed class ShellInboxItem
{
    /// <summary>唯一識別。</summary>
    public Guid Id { get; init; }

    /// <summary>接收時間（含時區偏移）。</summary>
    public DateTimeOffset ReceivedAt { get; init; }

    /// <summary>標題文字。</summary>
    public string TitleText { get; init; } = string.Empty;

    /// <summary>本文。</summary>
    public string MessageText { get; init; } = string.Empty;

    /// <summary>通知層級。</summary>
    public ShellNotificationLevel Level { get; init; } = ShellNotificationLevel.Information;

    /// <summary>是否已讀。</summary>
    public bool IsRead { get; set; }

    /// <summary>以目前文化格式化的本地接收時間字串。</summary>
    public string FormattedTimeLocal
        => ReceivedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
}
