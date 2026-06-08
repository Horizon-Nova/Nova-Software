using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Helix_Studio.Model;
using Helix_Studio.Infrastructure.Paths;

namespace Helix_Studio.Data.Repositories;

/// <summary>
/// 事件日誌資料存取（JSONL）。
/// </summary>
public sealed class EventLogRepository : IDisposable
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
    /// 建立事件日誌 Repository，並監看日誌檔案變動。
    /// </summary>
    public EventLogRepository()
    {
        var logDirectoryPath = AppPathProvider.QueryLogsDirectory();
        _logFilePath = Path.Combine(logDirectoryPath, LogFileName);
        using var _ = File.Open(_logFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        AttachWatcher(logDirectoryPath);
    }

    /// <summary>
    /// 記錄一筆日誌。
    /// </summary>
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
            File.AppendAllText(_logFilePath, line + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 查詢日誌清單（由新到舊）。
    /// </summary>
    public IReadOnlyList<EventLogEntryModel> QueryEntries()
        => File.ReadLines(_logFilePath, Encoding.UTF8)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Select(static line => JsonSerializer.Deserialize<EventLogEntryModel>(line, JsonOptions)
                ?? throw new FormatException("事件日誌格式錯誤。"))
            .OrderByDescending(static entry => entry.TimestampUtc)
            .ToList();

    /// <summary>
    /// 依保留期限清理過期記錄。
    /// </summary>
    /// <param name="cutoffUtc">保留期限的 UTC 截止時間。</param>
    public void ApplyRetention(DateTime cutoffUtc)
    {
        lock (_gate)
        {
            var retained = File.ReadLines(_logFilePath, Encoding.UTF8)
                .Where(static line => !string.IsNullOrWhiteSpace(line))
                .Select(static line => JsonSerializer.Deserialize<EventLogEntryModel>(line, JsonOptions))
                .Where(entry => entry is not null && entry.TimestampUtc >= cutoffUtc)
                .Select(static entry => JsonSerializer.Serialize(entry, JsonOptions))
                .ToArray();

            File.WriteAllLines(_logFilePath, retained, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 取得日誌檔案路徑。
    /// </summary>
    public string QueryLogFilePath() => _logFilePath;

    /// <summary>
    /// 釋放檔案監看資源。
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
