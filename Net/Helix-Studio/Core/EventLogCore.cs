using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Helix_Studio.Model;

namespace Helix_Studio.Core;

/// <summary>
/// 事件日誌的檔案寫入與讀取核心。
/// </summary>
public sealed class EventLogCore : IDisposable
{
    private const string LogFileName = "eventlog.jsonl";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly string _logFilePath;
    private readonly object _gate = new();
    private FileSystemWatcher? _watcher;

    /// <summary>
    /// 事件日誌檔案變更事件。
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// 建立事件日誌核心並掛上檔案監控。
    /// </summary>
    public EventLogCore()
    {
        var logDirectoryPath = AppPathCore.QueryLogsDirectory();
        _logFilePath = Path.Combine(logDirectoryPath, LogFileName);
        Directory.CreateDirectory(logDirectoryPath);
        using var _ = File.Open(_logFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        AttachWatcher(logDirectoryPath);
    }

    /// <summary>
    /// 記錄一筆事件日誌。
    /// </summary>
    /// <param name="level">等級。</param>
    /// <param name="source">來源。</param>
    /// <param name="message">訊息。</param>
    public void Record(EventLogLevel level, string source, string message)
    {
        var entry = new EventLogEntryModel
        {
            TimestampUtc = DateTime.UtcNow,
            Level = level,
            Source = source ?? string.Empty,
            Message = message ?? string.Empty,
        };

        var line = JsonSerializer.Serialize(entry, JsonOptions);

        lock (_gate)
        {
            File.AppendAllText(_logFilePath, line + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }
    }

    /// <summary>
    /// 查詢所有事件日誌項目（由新到舊）。
    /// </summary>
    public IReadOnlyList<EventLogEntryModel> QueryEntries()
        => File.ReadLines(_logFilePath, Encoding.UTF8)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Select(static line => JsonSerializer.Deserialize<EventLogEntryModel>(line, JsonOptions)
                ?? throw new FormatException("事件日誌格式不正確。"))
            .OrderByDescending(static entry => entry.TimestampUtc)
            .ToList();

    /// <summary>
    /// 查詢事件日誌檔案路徑。
    /// </summary>
    public string QueryLogFilePath() => _logFilePath;

    /// <summary>
    /// 釋放檔案監控。
    /// </summary>
    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    private void AttachWatcher(string logDirectoryPath)
    {
        _watcher?.Dispose();

        _watcher = new FileSystemWatcher(logDirectoryPath)
        {
            Filter = LogFileName,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true,
        };

        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Renamed += OnChanged;
    }

    private void OnChanged(object? sender, FileSystemEventArgs e)
        => Changed?.Invoke(this, EventArgs.Empty);
}

