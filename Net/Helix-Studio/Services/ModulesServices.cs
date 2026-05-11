using System;
using System.Collections.Generic;
using Helix_Studio.Domain;
using Helix_Studio.Model;

namespace Helix_Studio.Services;

/// <summary>
/// 模組頁面的零邏輯轉發服務。
/// </summary>
public sealed class ModulesServices(ModulesDomain domain)
{
    /// <summary>
    /// 模組清單變更事件。
    /// </summary>
    public event EventHandler? Changed
    {
        add => domain.Changed += value;
        remove => domain.Changed -= value;
    }

    /// <summary>
    /// 查詢目前模組目錄路徑。
    /// </summary>
    public string QueryModulesDirectoryPath() => domain.QueryModulesDirectoryPath();

    /// <summary>
    /// 查詢已安裝模組清單。
    /// </summary>
    public IReadOnlyList<ModulesInstalledItemModel> QueryModules() => domain.QueryModules();

    /// <summary>
    /// 導入模組。
    /// </summary>
    /// <param name="sourceDirectoryPath">來源資料夾路徑。</param>
    public ModulesInstalledItemModel ImportModule(string sourceDirectoryPath) => domain.ImportModule(sourceDirectoryPath);

    /// <summary>
    /// 停用模組。
    /// </summary>
    /// <param name="moduleId">模組識別值。</param>
    public void DisableModule(string moduleId) => domain.DisableModule(moduleId);

    /// <summary>
    /// 移除模組。
    /// </summary>
    /// <param name="moduleId">模組識別值。</param>
    public void UninstallModule(string moduleId) => domain.UninstallModule(moduleId);
}

