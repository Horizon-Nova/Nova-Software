using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Helix_Studio.Core;
using Helix_Studio.Model;

namespace Helix_Studio.Domain;

/// <summary>
/// 設定頁面的功能邏輯與資料組合。
/// </summary>
public sealed class SettingsDomain(IniCore iniCore, WorkspacesCore workspacesCore, ModulesCore modulesCore)
{
    private const string MainSection = "Main";
    private const string SettingsSection = "Settings";
    private const string ZhHantLanguageCode = "zh-Hant";

    /// <summary>
    /// 設定值變更事件。
    /// </summary>
    public event EventHandler? SettingsChanged
    {
        add => iniCore.Changed += value;
        remove => iniCore.Changed -= value;
    }

    /// <summary>
    /// 工作區變更事件。
    /// </summary>
    public event EventHandler? WorkspacesChanged
    {
        add => workspacesCore.Changed += value;
        remove => workspacesCore.Changed -= value;
    }

    /// <summary>
    /// 模組清單變更事件。
    /// </summary>
    public event EventHandler? ModulesChanged
    {
        add => modulesCore.Changed += value;
        remove => modulesCore.Changed -= value;
    }

    /// <summary>
    /// 查詢設定值快照。
    /// </summary>
    public SettingsModel QuerySettings()
        => iniCore.QuerySnapshot<SettingsModel>(SettingsSection);

    /// <summary>
    /// 儲存設定值快照。
    /// </summary>
    /// <param name="settings">欲儲存的設定值快照。</param>
    public void SaveSettings(SettingsModel settings)
        => iniCore.SaveSnapshot(SettingsSection, settings);

    /// <summary>
    /// 查詢設定頁面的工作區卡片資料。
    /// </summary>
    public IReadOnlyList<SettingsWorkspaceItemModel> QueryWorkspaces()
    {
        var activeWorkspaceName = QueryActiveWorkspaceName();

        return workspacesCore.QueryWorkspaces()
            .Select(workspaceName => new SettingsWorkspaceItemModel
            {
                Name = workspaceName,
                Description = Path.Combine(workspacesCore.QueryWorkspacesDirectoryPath(), workspaceName),
                IsActive = string.Equals(workspaceName, activeWorkspaceName, StringComparison.OrdinalIgnoreCase),
            })
            .ToList();
    }

    /// <summary>
    /// 切換目前作用中的工作區。
    /// </summary>
    /// <param name="workspaceName">欲切換的工作區名稱。</param>
    public void ActivateWorkspace(string workspaceName)
        => iniCore.SaveSnapshot(MainSection, new MainModel { Workspace = workspaceName });

    /// <summary>
    /// 刪除指定工作區。
    /// </summary>
    /// <param name="workspaceName">欲刪除的工作區名稱。</param>
    public void DeleteWorkspace(string workspaceName)
    {
        var workspaceDirectoryPath = Path.Combine(workspacesCore.QueryWorkspacesDirectoryPath(), workspaceName);
        Directory.Delete(workspaceDirectoryPath, true);

        var remainingWorkspaceName = workspacesCore.QueryWorkspaces()
            .Where(name => !string.Equals(name, workspaceName, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault() ?? string.Empty;

        if (string.Equals(QueryActiveWorkspaceName(), workspaceName, StringComparison.OrdinalIgnoreCase))
        {
            iniCore.SaveSnapshot(MainSection, new MainModel { Workspace = remainingWorkspaceName });
        }
    }

    /// <summary>
    /// 查詢設定頁面的模組卡片資料。
    /// </summary>
    public IReadOnlyList<SettingsModuleCardModel> QueryModuleSettingsCards()
        => modulesCore.QueryModules()
            .Select(module => new SettingsModuleCardModel
            {
                Id = module.Id,
                DisplayName = module.DisplayName,
                Version = module.Version,
                Description = module.Description,
                Icon = null,
                HasIcon = false,
                SettingsContent = null,
                HasSettingsContent = false,
            })
            .ToList();

    /// <summary>
    /// 查詢設定頁面的關於資訊。
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
    /// 匯出目前日誌為壓縮檔。
    /// </summary>
    /// <param name="exportDirectoryPath">壓縮檔輸出資料夾。</param>
    public void ExportLogs(string exportDirectoryPath)
    {
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
    }

    /// <summary>
    /// 開啟法律文件。
    /// </summary>
    public void OpenLegalNotices()
    {
        var languageCode = ResolveLanguageCode(QuerySettings().LanguageIndex);
        var legalFilePath = AppPathCore.QueryLegalNoticesFilePath(languageCode);
        Process.Start(new ProcessStartInfo
        {
            FileName = legalFilePath,
            UseShellExecute = true,
        });
    }

    private string QueryActiveWorkspaceName()
        => iniCore.QuerySnapshot<MainModel>(MainSection).Workspace ?? string.Empty;

    private static string ResolveLanguageCode(int languageIndex)
        => languageIndex == 0 ? ZhHantLanguageCode : "en";
}
