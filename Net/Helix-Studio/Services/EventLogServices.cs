using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Helix_Studio.Model.EventLog;

namespace Helix_Studio.Services;

/// <summary>應用程式內部事件日誌的收集、查詢、檔案輸出與保留清理。</summary>
public class EventLogServices
{
    private static readonly EventLogServices _instance = new();

    /// <summary>單例實例，供非靜態成員與內部狀態存取使用。</summary>
    public  static          EventLogServices Instance  => _instance;

    private readonly List<LogEntry> _entries = new();
    private int _nextId = 1;
    private const int MaxEntries = 2000;

    private LogLevel _minFileLevel = LogLevel.Info;
    private int _retentionDays = 30;

    private static readonly string LogDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "HorizonNovaStudio", "Logs");

    /// <summary>當有符合記錄條件的新日誌項目加入緩衝時觸發。</summary>
    public event Action? EntryAdded;

    /// <summary>寫入指定層級的日誌訊息至記憶體緩衝，並在設定允許時寫入檔案。</summary>
    public static void Log(LogLevel level, string message, string source = "Shell")
        => Instance.Add(level, message, source);

    /// <summary>寫入資訊層級日誌的便捷方法。</summary>
    public static void Info   (string msg, string src = "Shell") => Log(LogLevel.Info,    msg, src);

    /// <summary>寫入警告層級日誌的便捷方法。</summary>
    public static void Warning(string msg, string src = "Shell") => Log(LogLevel.Warning, msg, src);

    /// <summary>寫入錯誤層級日誌的便捷方法。</summary>
    public static void Error  (string msg, string src = "Shell") => Log(LogLevel.Error,   msg, src);

    /// <summary>寫入偵錯層級日誌的便捷方法。</summary>
    public static void Debug  (string msg, string src = "Shell") => Log(LogLevel.Debug,   msg, src);

    /// <summary>新增一筆日誌；若層級低於目前檔案記錄閾值則略過寫入檔案。</summary>
    public void Add(LogLevel level, string message, string source = "Shell")
    {
        if (!ShouldRecord(level)) return;

        lock (_entries)
        {
            if (_entries.Count >= MaxEntries)
                _entries.RemoveAt(0);

            _entries.Add(new LogEntry
            {
                Id        = _nextId++,
                Timestamp = DateTime.Now,
                Level     = level,
                Source    = source,
                Message   = message,
            });
        }

        TryWriteToFile(level, message, source);
        EntryAdded?.Invoke();
    }

    /// <summary>取得依時間新到舊排序的日誌副本（執行緒安全）。</summary>
    public List<LogEntry> GetAll()
    {
        lock (_entries) return [.. _entries.OrderByDescending(e => e.Timestamp)];
    }

    /// <summary>依可選的層級與關鍵字篩選日誌，結果依時間新到舊排序。</summary>
    public List<LogEntry> GetFiltered(LogLevel? level = null, string? keyword = null)
    {
        lock (_entries)
        {
            var q = _entries.AsEnumerable();
            if (level.HasValue)   q = q.Where(e => e.Level == level.Value);
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.ToLowerInvariant();
                q = q.Where(e => e.Message.Contains(kw, StringComparison.OrdinalIgnoreCase)
                               || e.Source.Contains(kw, StringComparison.OrdinalIgnoreCase));
            }
            return [.. q.OrderByDescending(e => e.Timestamp)];
        }
    }

    /// <summary>清除記憶體內所有日誌項目。</summary>
    public void Clear()
    {
        lock (_entries) _entries.Clear();
    }

    /// <summary>設定寫入檔案的最低層級與舊檔保留天數，並觸發過期日誌檔刪除。</summary>
    public void ConfigureFileLogging(LogLevel minLevel, int retentionDays)
    {
        _minFileLevel = minLevel;
        _retentionDays = retentionDays <= 0 ? 30 : retentionDays;
        CleanupOldFiles();
    }

    private void TryWriteToFile(LogLevel level, string message, string source)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var path = Path.Combine(LogDir, $"{DateTime.Now:yyyy-MM-dd}.log");
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\t{level}\t{source}\t{message}{Environment.NewLine}";
            File.AppendAllText(path, line, Encoding.UTF8);
        }
        catch
        {
        }
    }

    private bool ShouldRecord(LogLevel level)
    {
        if (_minFileLevel == LogLevel.Debug) return true;
        if (level == LogLevel.Debug) return false;
        return level >= _minFileLevel;
    }

    private void CleanupOldFiles()
    {
        try
        {
            if (!Directory.Exists(LogDir)) return;

            var cutoff = DateTime.Now.Date.AddDays(-_retentionDays);
            foreach (var file in Directory.GetFiles(LogDir, "*.log"))
            {
                var writeTime = File.GetLastWriteTime(file);
                if (writeTime.Date < cutoff)
                    File.Delete(file);
            }
        }
        catch
        {
        }
    }
}

