using System;

namespace Helix_Studio.Model;

/// <summary>
/// 事件日誌等級。
/// </summary>
public enum EventLogLevel
{
    /// <summary>
    /// 資訊。
    /// </summary>
    Info,

    /// <summary>
    /// 警告。
    /// </summary>
    Warning,

    /// <summary>
    /// 錯誤。
    /// </summary>
    Error,

    /// <summary>
    /// 偵錯。
    /// </summary>
    Debug,
}

/// <summary>
/// 事件日誌項目。
/// </summary>
public sealed record EventLogEntryModel
{
    /// <summary>
    /// 時間戳記（UTC）。
    /// </summary>
    public DateTime TimestampUtc { get; init; }

    /// <summary>
    /// 來源。
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// 等級。
    /// </summary>
    public EventLogLevel Level { get; init; }

    /// <summary>
    /// 訊息。
    /// </summary>
    public string Message { get; init; } = string.Empty;
}

