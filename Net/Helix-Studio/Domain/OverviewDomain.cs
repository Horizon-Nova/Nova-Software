using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Helix_Studio.Data.Repositories;
using Helix_Studio.Model;
using Helix_Studio.Infrastructure.Paths;

namespace Helix_Studio.Domain;

/// <summary>
/// 概覽頁 Domain（提供概覽頁所需資料）。
/// </summary>
public sealed class OverviewDomain(
    IniRepository iniRepository,
    WorkspacesRepository workspacesRepository,
    ModulesRepository modulesRepository)
{
    private const string MainSection = "Main";

    /// <summary>
    /// 概覽頁資料變更事件。
    /// </summary>
    public event EventHandler? Changed
    {
        add
        {
            iniRepository.Changed += value;
            workspacesRepository.Changed += value;
            modulesRepository.Changed += value;
        }
        remove
        {
            iniRepository.Changed -= value;
            workspacesRepository.Changed -= value;
            modulesRepository.Changed -= value;
        }
    }

    /// <summary>
    /// 取得概覽頁快照。
    /// </summary>
    public OverviewModel QueryOverview()
    {
        var machineName = Environment.MachineName;
        var osDescription = RuntimeInformation.OSDescription.Trim();

        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var uptimeText = FormatDuration(uptime);

        var configuredWorkspaceName = iniRepository.QuerySnapshot<MainModel>(MainSection).Workspace ?? string.Empty;
        var workspaceName = workspacesRepository.ResolveWorkspaceNameOrFallback(configuredWorkspaceName);
        var workspaceDescription = string.IsNullOrWhiteSpace(workspaceName)
            ? string.Empty
            : workspacesRepository.QueryWorkspaceDescription(workspaceName);

        var modules = QueryModules();

        var versionText = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;

        return new OverviewModel
        {
            MachineName = machineName,
            SubtitleText = string.Empty,
            OsInfoShort = osDescription,
            OsInfoFull = $"{osDescription} ({RuntimeInformation.OSArchitecture})",
            UptimeText = uptimeText,
            WorkspaceName = workspaceName,
            WorkspaceDescription = workspaceDescription,
            ModuleCountText = modules.Count.ToString(CultureInfo.InvariantCulture),
            ModuleStatusText = string.Empty,
            BuildVersionText = versionText,
            DataFolderPath = AppPathProvider.QueryAppDataDirectory(),
            Modules = modules,
        };
    }

    private IReadOnlyList<OverviewModuleItemModel> QueryModules()
        => modulesRepository.QueryModules()
            .Select(module => new OverviewModuleItemModel
            {
                Id = module.Id,
                DisplayName = module.DisplayName,
                Version = module.Version,
                Description = module.Description ?? string.Empty,
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
        if (days > 0)
        {
            parts.Add($"{days.ToString(CultureInfo.InvariantCulture)} 天");
        }

        if (hours > 0 || parts.Count > 0)
        {
            parts.Add($"{hours.ToString(CultureInfo.InvariantCulture)} 小時");
        }

        parts.Add($"{minutes.ToString(CultureInfo.InvariantCulture)} 分鐘");
        return string.Join(" ", parts);
    }
}
