using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Helix_Studio.Core;
using Helix_Studio.Model;

namespace Helix_Studio.Domain;

/// <summary>
/// 事件日誌頁面的功能邏輯與資料組合。
/// </summary>
public sealed class EventLogDomain(EventLogCore logCore, IniCore iniCore)
{
    private const string SettingsSection = "Settings";

    /// <summary>
    /// 事件日誌變更事件。
    /// </summary>
    public event EventHandler? Changed
    {
        add => logCore.Changed += value;
        remove => logCore.Changed -= value;
    }

    /// <summary>
    /// 記錄資訊日誌。
    /// </summary>
    /// <param name="source">來源。</param>
    /// <param name="message">訊息。</param>
    public void RecordInfo(string source, string message) => logCore.Record(EventLogLevel.Info, source, message);

    /// <summary>
    /// 記錄警告日誌。
    /// </summary>
    /// <param name="source">來源。</param>
    /// <param name="message">訊息。</param>
    public void RecordWarning(string source, string message) => logCore.Record(EventLogLevel.Warning, source, message);

    /// <summary>
    /// 記錄錯誤日誌。
    /// </summary>
    /// <param name="source">來源。</param>
    /// <param name="message">訊息。</param>
    public void RecordError(string source, string message) => logCore.Record(EventLogLevel.Error, source, message);

    /// <summary>
    /// 記錄偵錯日誌。
    /// </summary>
    /// <param name="source">來源。</param>
    /// <param name="message">訊息。</param>
    public void RecordDebug(string source, string message) => logCore.Record(EventLogLevel.Debug, source, message);

    /// <summary>
    /// 記錄未處理例外。
    /// </summary>
    /// <param name="source">來源。</param>
    /// <param name="exception">例外。</param>
    public void RecordUnhandledException(string source, Exception exception)
        => logCore.Record(EventLogLevel.Error, source, exception.ToString());

    /// <summary>
    /// 查詢事件日誌項目。
    /// </summary>
    public IReadOnlyList<EventLogEntryModel> QueryEntries() => logCore.QueryEntries();

    /// <summary>
    /// 匯出日誌壓縮檔到設定的匯出資料夾。
    /// </summary>
    /// <returns>輸出檔案路徑。</returns>
    public string ExportZip()
    {
        var settings = iniCore.QuerySnapshot<SettingsModel>(SettingsSection);
        var exportDirectoryPath = settings.LogExportFolder ?? string.Empty;
        Directory.CreateDirectory(exportDirectoryPath);

        var logDirectoryPath = AppPathCore.QueryLogsDirectory();
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
}

