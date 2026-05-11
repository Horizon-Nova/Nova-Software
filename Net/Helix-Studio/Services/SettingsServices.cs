using System;
using System.Collections.Generic;
using Helix_Studio.Domain;
using Helix_Studio.Model;

namespace Helix_Studio.Services;

/// <summary>
/// 設定頁面的零邏輯轉發服務。
/// </summary>
public sealed class SettingsServices(SettingsDomain domain)
{
    /// <summary>
    /// 設定值變更事件。
    /// </summary>
    public event EventHandler? SettingsChanged
    {
        add => domain.SettingsChanged += value;
        remove => domain.SettingsChanged -= value;
    }

    /// <summary>
    /// 工作區變更事件。
    /// </summary>
    public event EventHandler? WorkspacesChanged
    {
        add => domain.WorkspacesChanged += value;
        remove => domain.WorkspacesChanged -= value;
    }

    /// <summary>
    /// 模組變更事件。
    /// </summary>
    public event EventHandler? ModulesChanged
    {
        add => domain.ModulesChanged += value;
        remove => domain.ModulesChanged -= value;
    }

    /// <summary>
    /// 查詢設定值快照。
    /// </summary>
    public SettingsModel QuerySettings() => domain.QuerySettings();

    /// <summary>
    /// 儲存設定值快照。
    /// </summary>
    /// <param name="settings">欲儲存的設定值快照。</param>
    public void SaveSettings(SettingsModel settings) => domain.SaveSettings(settings);

    /// <summary>
    /// 查詢工作區卡片資料。
    /// </summary>
    public IReadOnlyList<SettingsWorkspaceItemModel> QueryWorkspaces() => domain.QueryWorkspaces();

    /// <summary>
    /// 切換目前作用中的工作區。
    /// </summary>
    /// <param name="workspaceName">欲切換的工作區名稱。</param>
    public void ActivateWorkspace(string workspaceName) => domain.ActivateWorkspace(workspaceName);

    /// <summary>
    /// 刪除指定工作區。
    /// </summary>
    /// <param name="workspaceName">欲刪除的工作區名稱。</param>
    public void DeleteWorkspace(string workspaceName) => domain.DeleteWorkspace(workspaceName);

    /// <summary>
    /// 查詢模組卡片資料。
    /// </summary>
    public IReadOnlyList<SettingsModuleCardModel> QueryModuleSettingsCards() => domain.QueryModuleSettingsCards();

    /// <summary>
    /// 查詢關於資訊。
    /// </summary>
    public SettingsAboutModel QueryAbout() => domain.QueryAbout();

    /// <summary>
    /// 匯出日誌壓縮檔。
    /// </summary>
    /// <param name="exportDirectoryPath">壓縮檔輸出資料夾。</param>
    public void ExportLogs(string exportDirectoryPath) => domain.ExportLogs(exportDirectoryPath);

    /// <summary>
    /// 開啟法律文件。
    /// </summary>
    public void OpenLegalNotices() => domain.OpenLegalNotices();
}
