using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using Helix_Studio.Infrastructure;
using Helix_Studio.Models;

namespace Helix_Studio.Services;

/// <summary>
/// 真實事件日誌：記錄系統與工具生命週期事件，持久化為 JSONL（逐行附加），
/// 啟動時載入。供 UI 即時顯示與工具 stdout 寫入。
/// </summary>
public sealed class EventLogService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private string? _workspaceId;

    /// <summary>最新在前的事件集合，供 UI 綁定。</summary>
    public ObservableCollection<EventLogEntry> Entries { get; } = new();

    /// <summary>設定工作區並載入既有日誌。</summary>
    public void Initialize(string workspaceId)
    {
        _workspaceId = workspaceId;
        Entries.Clear();

        var path = AppPathProvider.EventLogFile(workspaceId);
        if (!File.Exists(path)) return;

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            EventLogEntry? entry = null;
            try { entry = JsonSerializer.Deserialize<EventLogEntry>(line, JsonOpts); }
            catch (JsonException) { /* 略過毀損行 */ }
            if (entry is not null) Entries.Insert(0, entry);
        }
    }

    /// <summary>記錄一筆事件（執行緒安全：marshal 回 UI 執行緒更新集合）。</summary>
    public void Record(EventLevel level, EventCategory category, string source, string message)
    {
        var entry = new EventLogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Category = category,
            Source = source,
            Message = message,
        };

        Append(entry);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            Entries.Insert(0, entry);
        else
            dispatcher.Invoke(() => Entries.Insert(0, entry));
    }

    /// <summary>清空目前工作區的事件（記憶體集合與持久化檔一併清除）。</summary>
    public void Clear()
    {
        Entries.Clear();
        if (_workspaceId is null) return;
        var path = AppPathProvider.EventLogFile(_workspaceId);
        if (File.Exists(path)) File.Delete(path);
    }

    private void Append(EventLogEntry entry)
    {
        if (_workspaceId is null) return;
        var path = AppPathProvider.EventLogFile(_workspaceId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(path, JsonSerializer.Serialize(entry, JsonOpts) + Environment.NewLine);
    }
}
