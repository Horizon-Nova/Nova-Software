using System;
using System.Collections.Generic;
using Helix_Studio.Domain;
using Helix_Studio.Model;

namespace Helix_Studio.Services;

/// <summary>
/// 模組畫面服務（僅轉呼叫至 Domain）。
/// </summary>
public sealed class ModulesServices(ModulesDomain domain)
{
    /// <summary>
    /// 模組或相關 I/O 變更事件。
    /// </summary>
    public event EventHandler? Changed
    {
        add => domain.Changed += value;
        remove => domain.Changed -= value;
    }

    /// <summary>
    /// 查詢目前工作區的模組目錄路徑。
    /// </summary>
    public string QueryModulesDirectoryPath() => domain.QueryModulesDirectoryPath();

    /// <summary>
    /// 查詢已安裝模組清單。
    /// </summary>
    public IReadOnlyList<ModulesInstalledItemModel> QueryModules() => domain.QueryModules();

    /// <summary>
    /// 匯入模組。
    /// </summary>
    /// <param name="sourceDirectoryPath">來源模組資料夾。</param>
    public ModulesInstalledItemModel ImportModule(string sourceDirectoryPath) => domain.ImportModule(sourceDirectoryPath);

    /// <summary>
    /// 停用模組。
    /// </summary>
    /// <param name="moduleId">模組識別碼。</param>
    public void DisableModule(string moduleId) => domain.DisableModule(moduleId);

    /// <summary>
    /// 解除安裝模組。
    /// </summary>
    /// <param name="installedFolderPath">模組安裝資料夾。</param>
    public void UninstallModule(string installedFolderPath) => domain.UninstallModule(installedFolderPath);
}

