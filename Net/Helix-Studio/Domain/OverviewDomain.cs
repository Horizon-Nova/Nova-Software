using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Helix_Studio.Core;
using Helix_Studio.Model;

namespace Helix_Studio.Domain;

/// <summary>
/// 概覽頁面的功能邏輯與資料組合。
/// </summary>
public sealed class OverviewDomain(IniCore iniCore, WorkspacesCore workspacesCore, ModulesCore modulesCore)
{
    private const string MainSection = "Main";

    /// <summary>
    /// 概覽頁面資料變更事件。
    /// </summary>
    public event EventHandler? Changed
    {
        add
        {
            iniCore.Changed += value;
            workspacesCore.Changed += value;
            modulesCore.Changed += value;
        }
        remove
        {
            iniCore.Changed -= value;
            workspacesCore.Changed -= value;
            modulesCore.Changed -= value;
        }
    }

    /// <summary>
    /// 查詢概覽頁面資料快照。
    /// </summary>
    public OverviewModel QueryOverview()
    {
        var machineName = Environment.MachineName;
        var osDescription = RuntimeInformation.OSDescription.Trim();
        var osInfoShort = osDescription;
        var osInfoFull = $"{osDescription} ({RuntimeInformation.OSArchitecture})";

        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var uptimeText = $"開機時間：{FormatDuration(uptime)}";

        var workspaceName = iniCore.QuerySnapshot<MainModel>(MainSection).Workspace ?? string.Empty;
        var workspaceDescription = string.IsNullOrWhiteSpace(workspaceName)
            ? "尚未選取工作區"
            : System.IO.Path.Combine(workspacesCore.QueryWorkspacesDirectoryPath(), workspaceName);

        var modules = QueryModules();
        var moduleCountText = $"{modules.Count} 個模組";
        var moduleStatusText = modules.Count == 0 ? "尚未安裝模組" : "可用";

        var versionText = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "—";

        return new OverviewModel
        {
            MachineName = machineName,
            SubtitleText = "以下為系統、工作區與模組狀態摘要。",
            OsInfoShort = osInfoShort,
            OsInfoFull = osInfoFull,
            UptimeText = uptimeText,
            WorkspaceName = string.IsNullOrWhiteSpace(workspaceName) ? "—" : workspaceName,
            WorkspaceDescription = workspaceDescription,
            ModuleCountText = moduleCountText,
            ModuleStatusText = moduleStatusText,
            BuildVersionText = versionText,
            DataFolderPath = AppPathCore.QueryAppDataDirectory(),
            Modules = modules,
        };
    }

    private IReadOnlyList<OverviewModuleItemModel> QueryModules()
        => modulesCore.QueryModules()
            .Select(module => new OverviewModuleItemModel
            {
                Id = module.Id,
                DisplayName = module.DisplayName,
                Version = module.Version,
                Description = module.Description,
                Icon = null,
                HasIcon = false,
            })
            .OrderBy(static item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string FormatDuration(TimeSpan uptime)
    {
        var days = uptime.Days;
        var hours = uptime.Hours;
        var minutes = uptime.Minutes;

        var parts = new List<string>();
        if (days > 0) parts.Add($"{days.ToString(CultureInfo.InvariantCulture)} 天");
        if (hours > 0 || parts.Count > 0) parts.Add($"{hours.ToString(CultureInfo.InvariantCulture)} 小時");
        parts.Add($"{minutes.ToString(CultureInfo.InvariantCulture)} 分鐘");
        return string.Join(" ", parts);
    }
}

