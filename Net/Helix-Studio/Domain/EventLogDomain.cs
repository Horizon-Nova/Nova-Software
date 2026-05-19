using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Helix_Studio.Data.Repositories;
using Helix_Studio.Model;
using Helix_Studio.Utils;

namespace Helix_Studio.Domain;

/// <summary>
/// 事件日誌 Domain（提供事件日誌頁所需資料與操作）。
/// </summary>
public sealed class EventLogDomain(
    EventLogRepository eventLogRepository,
    IniRepository iniRepository)
{
    private const string SettingsSection = "Settings";

    private static readonly IReadOnlyDictionary<int, int> RetentionDaysByIndex =
        new Dictionary<int, int>
        {
            [0] = 7,
            [1] = 30,
            [2] = 90,
            [3] = 180,
        };

    private static readonly IReadOnlyDictionary<int, IReadOnlySet<EventLogLevel>> EnabledLevelsByIndex =
        new Dictionary<int, IReadOnlySet<EventLogLevel>>
        {
            [0] = new HashSet<EventLogLevel> { EventLogLevel.Info, EventLogLevel.Warning, EventLogLevel.Error },
            [1] = new HashSet<EventLogLevel> { EventLogLevel.Warning, EventLogLevel.Error },
            [2] = new HashSet<EventLogLevel> { EventLogLevel.Error },
            [3] = new HashSet<EventLogLevel> { EventLogLevel.Debug, EventLogLevel.Info, EventLogLevel.Warning, EventLogLevel.Error },
        };

    /// <summary>
    /// 事件日誌變更事件。
    /// </summary>
    public event EventHandler? Changed
    {
        add => eventLogRepository.Changed += value;
        remove => eventLogRepository.Changed -= value;
    }

    /// <summary>
    /// 記錄日誌。
    /// </summary>
    public void Record(EventLogLevel level, string source, string message)
    {
        if (!QueryLevelEnabled(level))
        {
            return;
        }

        eventLogRepository.Record(level, source, message);
    }

    /// <summary>
    /// 查詢事件日誌項目（由新到舊）。
    /// </summary>
    public IReadOnlyList<EventLogEntryModel> QueryEntries()
        => eventLogRepository.QueryEntries();

    /// <summary>
    /// 匯出日誌 ZIP 到設定的匯出資料夾。
    /// </summary>
    public string ExportZip()
    {
        var settings = iniRepository.QuerySnapshot<SettingsModel>(SettingsSection);
        var exportDirectoryPath = settings.LogExportFolder ?? string.Empty;
        if (string.IsNullOrWhiteSpace(exportDirectoryPath))
        {
            throw new InvalidOperationException("尚未設定日誌匯出資料夾，請先至設定頁設定。");
        }

        Directory.CreateDirectory(exportDirectoryPath);

        var logDirectoryPath = AppPathHelper.QueryLogsDirectory();
        var archiveFileName = $"Helix-Studio-Logs-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
        var archiveFilePath = Path.Combine(exportDirectoryPath, archiveFileName);
        File.Delete(archiveFilePath);

        using var archive = ZipFile.Open(archiveFilePath, ZipArchiveMode.Create);
        foreach (var filePath in Directory.EnumerateFiles(logDirectoryPath, "*", SearchOption.AllDirectories))
        {
            var entryName = Path.GetRelativePath(logDirectoryPath, filePath);
            archive.CreateEntryFromFile(filePath, entryName);
        }

        return archiveFilePath;
    }

    /// <summary>
    /// 套用日誌層級與保留期限設定。
    /// </summary>
    public void ApplyMaintenance()
    {
        var settings = iniRepository.QuerySnapshot<SettingsModel>(SettingsSection);
        var retentionDays = RetentionDaysByIndex.TryGetValue(settings.LogRetentionIndex, out var days) ? days : 30;
        var cutoffUtc = DateTime.UtcNow.AddDays(-retentionDays);
        eventLogRepository.ApplyRetention(cutoffUtc);
    }

    private bool QueryLevelEnabled(EventLogLevel level)
    {
        var settings = iniRepository.QuerySnapshot<SettingsModel>(SettingsSection);
        var enabled = EnabledLevelsByIndex.TryGetValue(settings.LogLevelIndex, out var set)
            ? set
            : EnabledLevelsByIndex[0];

        return enabled.Contains(level);
    }
}
