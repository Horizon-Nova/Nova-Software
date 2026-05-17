using System;
using System.Collections.Generic;
using Helix_Studio.Core;
using Helix_Studio.Domain;
using Helix_Studio.Model;

namespace Helix_Studio.Services;

/// <summary>
/// MainWindow 服務（僅轉呼叫至 Domain）。
/// </summary>
public sealed class MainWindowServices(MainWindowDomain domain, ModuleHealthSink moduleHealthSink)
{
    /// <summary>
    /// 模組健康狀態變更事件。
    /// </summary>
    public event Action<string, ModuleHealth, string?>? ModuleHealthChanged
    {
        add => moduleHealthSink.Changed += value;
        remove => moduleHealthSink.Changed -= value;
    }

    /// <summary>
    /// 工作區 I/O 變更事件。
    /// </summary>
    public event EventHandler? WorkspacesChanged
    {
        add => domain.WorkspacesChanged += value;
        remove => domain.WorkspacesChanged -= value;
    }

    /// <summary>
    /// 模組 I/O 變更事件。
    /// </summary>
    public event EventHandler? ModulesChanged
    {
        add => domain.ModulesChanged += value;
        remove => domain.ModulesChanged -= value;
    }

    /// <summary>
    /// 狀態 I/O 變更事件。
    /// </summary>
    public event EventHandler? StateChanged
    {
        add => domain.StateChanged += value;
        remove => domain.StateChanged -= value;
    }

    /// <summary>
    /// 查詢工作區清單。
    /// </summary>
    public IReadOnlyList<string> QueryWorkspaces() => domain.QueryWorkspaces();

    /// <summary>
    /// 查詢目前啟用的工作區名稱。
    /// </summary>
    public string QueryActiveWorkspaceName() => domain.QueryActiveWorkspaceName();

    /// <summary>
    /// 查詢目前工作區的模組清單。
    /// </summary>
    public IReadOnlyList<ModuleModel> QueryModules() => domain.QueryModules();

    /// <summary>
    /// 切換工作區。
    /// </summary>
    /// <param name="workspaceName">工作區名稱。</param>
    public void ActivateWorkspace(string workspaceName) => domain.ActivateWorkspace(workspaceName);
}

