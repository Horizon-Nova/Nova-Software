using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Helix_Studio.Data.Repositories;
using Helix_Studio.Model;
using Helix_Studio.Utils;

namespace Helix_Studio.Domain;

/// <summary>
/// 設定 Domain（提供設定頁所需資料與操作）。
/// </summary>
public sealed class SettingsDomain(
    IniRepository iniRepository,
    WorkspacesRepository workspacesRepository)
{
    private const string MainSection = "Main";
    private const string SettingsSection = "Settings";
    private const string DefaultHomePageId = "overview";

    private static readonly IReadOnlyDictionary<int, TimeSpan> InactivityTimeoutByIndex =
        new Dictionary<int, TimeSpan>
        {
            [1] = TimeSpan.FromMinutes(5),
            [2] = TimeSpan.FromMinutes(15),
            [3] = TimeSpan.FromMinutes(30),
            [4] = TimeSpan.FromHours(1),
        };

    /// <summary>
    /// 設定值變更事件。
    /// </summary>
    public event EventHandler? SettingsChanged
    {
        add => iniRepository.Changed += value;
        remove => iniRepository.Changed -= value;
    }

    /// <summary>
    /// 工作區變更事件。
    /// </summary>
    public event EventHandler? WorkspacesChanged
    {
        add => workspacesRepository.Changed += value;
        remove => workspacesRepository.Changed -= value;
    }

    /// <summary>
    /// 查詢設定快照。
    /// </summary>
    public SettingsModel QuerySettings()
        => iniRepository.QuerySnapshot<SettingsModel>(SettingsSection);

    /// <summary>
    /// 儲存設定快照。
    /// </summary>
    public void SaveSettings(SettingsModel settings)
        => iniRepository.SaveValues(SettingsSection, ResolveValues(settings));

    /// <summary>
    /// 依閮剖???身摰??援 Home ?澆???撠汗隢??
    /// </summary>
    public string ResolveStartupPageId()
    {
        var settings = QuerySettings();
        var lastVisited = settings.LastVisitedPageTag ?? string.Empty;

        return settings.StartPageIndex == 1 && !string.IsNullOrWhiteSpace(lastVisited)
            ? lastVisited
            : DefaultHomePageId;
    }

    /// <summary>
    /// 儲存最後一次瀏覽的頁面代號（供「上次瀏覽頁面」啟動選項使用）。
    /// </summary>
    /// <param name="pageId">頁面代號。</param>
    public void SaveLastVisitedPageId(string pageId)
    {
        var normalized = string.IsNullOrWhiteSpace(pageId) ? string.Empty : pageId;
        var existing = iniRepository.QuerySnapshot<SettingsModel>(SettingsSection).LastVisitedPageTag ?? string.Empty;
        if (string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        iniRepository.SaveValues(SettingsSection, new Dictionary<string, string?>
        {
            [nameof(SettingsModel.LastVisitedPageTag)] = normalized,
        });
    }

    /// <summary>
    /// 解析閒置鎖定等待時間；回傳 <see langword="null"/> 代表停用。
    /// </summary>
    public TimeSpan? QueryInactivityTimeout()
    {
        var index = QuerySettings().InactivityIndex;
        return InactivityTimeoutByIndex.TryGetValue(index, out var timeout) ? timeout : null;
    }

    /// <summary>
    /// 查詢工作區清單（供設定頁顯示）。
    /// </summary>
    public IReadOnlyList<SettingsWorkspaceItemModel> QueryWorkspaces()
    {
        var activeWorkspaceName = QueryActiveWorkspaceName();
        var root = workspacesRepository.QueryWorkspacesDirectoryPath();

        return workspacesRepository.QueryWorkspaceNames()
            .Select(workspaceName => new SettingsWorkspaceItemModel
            {
                Name = workspaceName,
                Description = Path.Combine(root, workspaceName),
                IsActive = string.Equals(workspaceName, activeWorkspaceName, StringComparison.OrdinalIgnoreCase),
            })
            .ToList();
    }

    /// <summary>
    /// 切換目前作用中的工作區。
    /// </summary>
    public void ActivateWorkspace(string workspaceName)
        => iniRepository.SaveValues(MainSection, new Dictionary<string, string?>
        {
            [nameof(MainModel.Workspace)] = workspaceName,
        });

    /// <summary>
    /// 建立工作區並切換為使用中。
    /// </summary>
    public void CreateWorkspace(string workspaceName, string description)
    {
        workspacesRepository.CreateWorkspace(workspaceName, description);
        ActivateWorkspace(workspaceName);
    }

    /// <summary>
    /// 刪除工作區。
    /// </summary>
    public void DeleteWorkspace(string workspaceName)
    {
        var workspaceDirectoryPath = Path.Combine(workspacesRepository.QueryWorkspacesDirectoryPath(), workspaceName);
        Directory.Delete(workspaceDirectoryPath, recursive: true);

        var remainingWorkspaceName = workspacesRepository.QueryWorkspaceNames()
            .Where(name => !string.Equals(name, workspaceName, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault() ?? string.Empty;

        if (string.Equals(QueryActiveWorkspaceName(), workspaceName, StringComparison.OrdinalIgnoreCase))
        {
            iniRepository.SaveValues(MainSection, new Dictionary<string, string?>
            {
                [nameof(MainModel.Workspace)] = remainingWorkspaceName,
            });
        }
    }

    /// <summary>
    /// 查詢關於資訊。
    /// </summary>
    public SettingsAboutModel QueryAbout()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var versionText = assembly.GetName().Version?.ToString() ?? "—";
        var buildDateText = File.GetLastWriteTime(assembly.Location).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        return new SettingsAboutModel
        {
            VersionText = versionText,
            BuildDateText = buildDateText,
            LicenseExpiryText = "—",
        };
    }

    /// <summary>
    /// 匯出目前日誌到 ZIP（輸出到指定資料夾）。
    /// </summary>
    public string ExportLogs(string exportDirectoryPath)
    {
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
    /// 開啟法律文件。
    /// </summary>
    public void OpenLegalNotices(string languageCode)
    {
        var legalFilePath = AppPathHelper.QueryLegalNoticesFilePath(languageCode);
        Process.Start(new ProcessStartInfo
        {
            FileName = legalFilePath,
            UseShellExecute = true,
        });
    }

    private string QueryActiveWorkspaceName()
        => iniRepository.QuerySnapshot<MainModel>(MainSection).Workspace ?? string.Empty;

    private static IReadOnlyDictionary<string, string?> ResolveValues<TSnapshot>(TSnapshot snapshot)
        where TSnapshot : class
        => snapshot
            .GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property is { CanRead: true })
            .ToDictionary(
                property => property.Name,
                property => (string?)Convert.ToString(property.GetValue(snapshot), CultureInfo.InvariantCulture),
                StringComparer.OrdinalIgnoreCase);
}
