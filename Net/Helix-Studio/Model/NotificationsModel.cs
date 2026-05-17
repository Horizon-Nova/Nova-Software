using System;
using System.Globalization;

namespace Helix_Studio.Model.Notifications;

/// <summary>
/// 通知層級。
/// </summary>
public enum ShellNotificationLevel
{
    /// <summary>
    /// 一般資訊。
    /// </summary>
    Info,

    /// <summary>
    /// 成功。
    /// </summary>
    Success,

    /// <summary>
    /// 警告。
    /// </summary>
    Warning,

    /// <summary>
    /// 錯誤。
    /// </summary>
    Error,
}

/// <summary>
/// 通知收件匣項目模型。
/// </summary>
public sealed record ShellInboxItem
{
    /// <summary>
    /// 通知層級。
    /// </summary>
    public ShellNotificationLevel Level { get; init; }

    /// <summary>
    /// 標題文字。
    /// </summary>
    public string TitleText { get; init; } = string.Empty;

    /// <summary>
    /// 訊息文字。
    /// </summary>
    public string MessageText { get; init; } = string.Empty;

    /// <summary>
    /// UTC 時間戳記。
    /// </summary>
    public DateTimeOffset TimeUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 本機時間格式化文字。
    /// </summary>
    public string FormattedTimeLocal
        => TimeUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.CurrentCulture);
}

