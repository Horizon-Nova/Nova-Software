using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Helix_Studio.Core;
using Helix_Studio.Model;

namespace Helix_Studio.Domain;

/// <summary>
/// 模組功能 Domain。
/// </summary>
public sealed class ModulesDomain(IniCore iniCore, ModulesCore modulesCore)
{
    /// <summary>
    /// 模組或相關 I/O 變更事件。
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
    /// 查詢目前工作區的模組目錄路徑。
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
    /// 匯入模組。
    /// </summary>
    /// <param name="sourceDirectoryPath">來源模組資料夾。</param>
    public ModulesInstalledItemModel ImportModule(string sourceDirectoryPath)
        => QueryInstalledItem(modulesCore.ImportModule(sourceDirectoryPath));

    /// <summary>
    /// 停用模組。
    /// </summary>
    /// <param name="moduleId">模組識別碼。</param>
    public void DisableModule(string moduleId)
        => modulesCore.DisableModule(moduleId);

    /// <summary>
    /// 解除安裝模組（刪除安裝資料夾）。
    /// </summary>
    /// <param name="installedFolderPath">模組安裝資料夾。</param>
    public void UninstallModule(string installedFolderPath)
        => modulesCore.DeleteModule(installedFolderPath);

    private ModulesInstalledItemModel QueryInstalledItem(ModuleModel module)
    {
        var readmeText = QueryReadmeText(module.FolderPath);
        var statusText = module.RequiresLicense ? "需要授權" : "可使用";

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

