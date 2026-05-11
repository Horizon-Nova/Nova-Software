using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Helix_Studio.Core;
using Helix_Studio.Model;

namespace Helix_Studio.Domain;

/// <summary>
/// 模組頁面的功能邏輯與資料組合。
/// </summary>
public sealed class ModulesDomain(IniCore iniCore, ModulesCore modulesCore)
{
    /// <summary>
    /// 模組清單變更事件。
    /// </summary>
    public event EventHandler? Changed
    {
        add
        {
            modulesCore.Changed += value;
            iniCore.Changed += value;
        }
        remove
        {
            modulesCore.Changed -= value;
            iniCore.Changed -= value;
        }
    }

    /// <summary>
    /// 查詢目前模組目錄路徑。
    /// </summary>
    public string QueryModulesDirectoryPath()
        => modulesCore.QueryModulesDirectoryPath();

    /// <summary>
    /// 查詢已安裝模組清單。
    /// </summary>
    public IReadOnlyList<ModulesInstalledItemModel> QueryModules()
        => modulesCore.QueryModules()
            .Select(QueryInstalledItem)
            .OrderBy(static item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// 導入模組。
    /// </summary>
    /// <param name="sourceDirectoryPath">來源資料夾路徑。</param>
    public ModulesInstalledItemModel ImportModule(string sourceDirectoryPath)
        => QueryInstalledItem(modulesCore.ImportModule(sourceDirectoryPath));

    /// <summary>
    /// 停用模組。
    /// </summary>
    /// <param name="moduleId">模組識別值。</param>
    public void DisableModule(string moduleId)
        => modulesCore.DisableModule(moduleId);

    /// <summary>
    /// 移除模組。
    /// </summary>
    /// <param name="moduleId">模組識別值。</param>
    public void UninstallModule(string moduleId)
    {
        var module = modulesCore.QueryModules()
            .FirstOrDefault(model => string.Equals(model.Id, moduleId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("找不到欲移除的模組。");

        modulesCore.DeleteModule(module.FolderPath);
    }

    private ModulesInstalledItemModel QueryInstalledItem(ModuleModel module)
    {
        var readmeText = QueryReadmeText(module.FolderPath);
        var statusText = module.RequiresLicense ? "需要授權" : "可用";

        return new ModulesInstalledItemModel
        {
            Id = module.Id,
            DisplayName = module.DisplayName,
            Version = module.Version,
            Author = module.Author,
            Description = module.Description,
            MinHostVersion = module.MinHostVersion,
            InstalledFolderPath = module.FolderPath,
            StatusText = statusText,
            Icon = null,
            ReadmeText = readmeText,
        };
    }

    private static string QueryReadmeText(string moduleDirectoryPath)
    {
        var readmePath = Directory.EnumerateFiles(moduleDirectoryPath, "*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(path =>
            {
                var name = Path.GetFileName(path);
                return name.Equals("README.md", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("README.txt", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("README", StringComparison.OrdinalIgnoreCase);
            });

        if (readmePath is null)
        {
            return string.Empty;
        }

        return File.ReadAllText(readmePath, Encoding.UTF8);
    }
}

