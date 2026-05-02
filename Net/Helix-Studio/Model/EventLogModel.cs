namespace Helix_Studio.Model.EventLog;

/// <summary>事件日誌訊息層級。</summary>
public enum LogLevel
{
    /// <summary>一般資訊。</summary>
    Info,

    /// <summary>警告。</summary>
    Warning,

    /// <summary>錯誤。</summary>
    Error,

    /// <summary>偵錯。</summary>
    Debug
}

/// <summary>單筆事件日誌條目。</summary>
public class LogEntry
{
    /// <summary>資料庫或序列識別。</summary>
    public int       Id        { get; init; }

    /// <summary>事件時間（本地建立時間）。</summary>
    public DateTime  Timestamp { get; init; } = DateTime.Now;

    /// <summary>訊息層級。</summary>
    public LogLevel  Level     { get; init; } = LogLevel.Info;

    /// <summary>來源模組或元件名稱。</summary>
    public string    Source    { get; init; } = "Shell";

    /// <summary>日誌訊息本文。</summary>
    public string    Message   { get; init; } = string.Empty;

    /// <summary>格式化後之時間戳記顯示字串。</summary>
    public string TimestampText => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>層級之顯示文字（大寫）。</summary>
    public string LevelText     => Level.ToString().ToUpper();
}
