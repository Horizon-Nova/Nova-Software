using System;
using System.Collections.Generic;
using System.Linq;
using Helix_Studio.Data.Repositories;
using Helix_Studio.Model;

namespace Helix_Studio.Domain;

/// <summary>
/// 主視窗領域邏輯。
/// </summary>
public sealed class MainWindowDomain(
    IniRepository iniRepository,
    WorkspacesRepository workspacesRepository,
    ModulesRepository modulesRepository)
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
    /// 設定檔變更事件。
    /// </summary>
    public event EventHandler? StateChanged
    {
        add => iniRepository.Changed += value;
        remove => iniRepository.Changed -= value;
    }

    /// <summary>
    /// 工作區目錄變更事件。
    /// </summary>
    public event EventHandler? WorkspacesChanged
    {
        add => workspacesRepository.Changed += value;
        remove => workspacesRepository.Changed -= value;
    }

    /// <summary>
    /// 模組目錄變更事件。
    /// </summary>
    public event EventHandler? ModulesChanged
    {
        add => modulesRepository.Changed += value;
        remove => modulesRepository.Changed -= value;
    }

    /// <summary>
    /// 查詢主視窗標題。
    /// </summary>
    public string QueryWindowTitle() => "Helix Studio";

    /// <summary>
    /// 查詢工作區名稱清單。
    /// </summary>
    public IReadOnlyList<string> QueryWorkspaces()
        => workspacesRepository.QueryWorkspaceNames();

    /// <summary>
    /// 查詢工作區描述。
    /// </summary>
    public string QueryWorkspaceDescription(string workspaceName)
        => workspacesRepository.QueryWorkspaceDescription(workspaceName);

    /// <summary>
    /// 查詢使用中的工作區名稱。
    /// </summary>
    public string QueryActiveWorkspaceName()
        => iniRepository.QuerySnapshot<MainModel>(MainSection).Workspace ?? string.Empty;

    /// <summary>
    /// 查詢使用中的工作區描述。
    /// </summary>
    public string QueryActiveWorkspaceDescription()
    {
        var active = QueryActiveWorkspaceName();
        return string.IsNullOrWhiteSpace(active)
            ? string.Empty
            : workspacesRepository.QueryWorkspaceDescription(active);
    }

    /// <summary>
    /// 查詢目前工作區的模組清單。
    /// </summary>
    public IReadOnlyList<ModuleModel> QueryModules()
        => modulesRepository.QueryModules();

    /// <summary>
    /// 切換使用中的工作區。
    /// </summary>
    public void ActivateWorkspace(string workspaceName)
        => ActivateWorkspace(workspaceName, QueryWorkspaceDescription(workspaceName));

    /// <summary>
    /// 切換使用中的工作區並寫入描述。
    /// </summary>
    public void ActivateWorkspace(string workspaceName, string workspaceDescription)
        => iniRepository.SaveValues(MainSection, new Dictionary<string, string?>
        {
            [nameof(MainModel.Workspace)] = workspaceName,
            [nameof(MainModel.WorkspaceDescription)] = workspaceDescription,
        });

    public string ResolveStartupPageId()
    {
        var settings = iniRepository.QuerySnapshot<SettingsModel>(SettingsSection);
        var lastVisited = settings.LastVisitedPageTag ?? string.Empty;

        return settings.StartPageIndex == 1 && !string.IsNullOrWhiteSpace(lastVisited)
            ? lastVisited
            : DefaultHomePageId;
    }

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

    public TimeSpan? QueryInactivityTimeout()
    {
        var index = iniRepository.QuerySnapshot<SettingsModel>(SettingsSection).InactivityIndex;
        return InactivityTimeoutByIndex.TryGetValue(index, out var timeout) ? timeout : null;
    }
}
