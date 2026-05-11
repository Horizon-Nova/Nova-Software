using System;
using System.Collections.Generic;
using Helix_Studio.Core;
using Helix_Studio.Model;

namespace Helix_Studio.Domain;

/// <summary>
/// MainWindow 的業務入口，提供工作區、模組與主設定狀態的查詢與切換。
/// </summary>
public sealed class MainWindowDomain(IniCore iniCore, WorkspacesCore workspacesCore, ModulesCore modulesCore)
{
    private const string MainSection = "Main";

    /// <summary>
    /// 轉發工作區資料夾的 I/O 變更通知。
    /// </summary>
    public event EventHandler? WorkspacesChanged
    {
        add => workspacesCore.Changed += value;
        remove => workspacesCore.Changed -= value;
    }

    /// <summary>
    /// 轉發模組資料夾的 I/O 變更通知。
    /// </summary>
    public event EventHandler? ModulesChanged
    {
        add => modulesCore.Changed += value;
        remove => modulesCore.Changed -= value;
    }

    /// <summary>
    /// 轉發主設定 INI 的 I/O 變更通知。
    /// </summary>
    public event EventHandler? StateChanged
    {
        add => iniCore.Changed += value;
        remove => iniCore.Changed -= value;
    }

    /// <summary>
    /// 查詢目前可用的工作區名稱清單。
    /// </summary>
    public IReadOnlyList<string> QueryWorkspaces()
        => workspacesCore.QueryWorkspaces();

    /// <summary>
    /// 查詢目前主設定指定的工作區名稱。
    /// </summary>
    public string QueryActiveWorkspaceName()
        => iniCore.QuerySnapshot<MainModel>(MainSection).Workspace ?? string.Empty;

    /// <summary>
    /// 查詢目前工作區下的模組清單。
    /// </summary>
    public IReadOnlyList<ModuleModel> QueryModules()
        => modulesCore.QueryModules();

    /// <summary>
    /// 啟用指定工作區並寫回主設定。
    /// </summary>
    /// <param name="workspaceName">欲啟用的工作區名稱。</param>
    public void ActivateWorkspace(string workspaceName)
        => iniCore.SaveSnapshot(MainSection, new MainModel { Workspace = workspaceName });
}
