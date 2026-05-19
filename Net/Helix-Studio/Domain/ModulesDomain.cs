using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Helix_Studio.Data.Repositories;
using Helix_Studio.Model;

namespace Helix_Studio.Domain;

/// <summary>
/// 模組 Domain（提供模組管理頁所需資料與操作）。
/// </summary>
public sealed class ModulesDomain(
    ModulesRepository modulesRepository,
    IniRepository iniRepository)
{
    /// <summary>
    /// 模組或工作區變更事件。
    /// </summary>
    public event EventHandler? Changed
    {
        add
        {
            modulesRepository.Changed += value;
            iniRepository.Changed += value;
        }
        remove
        {
            modulesRepository.Changed -= value;
            iniRepository.Changed -= value;
        }
    }

    /// <summary>
    /// 查詢目前工作區的模組目錄路徑。
    /// </summary>
    public string QueryModulesDirectoryPath()
        => modulesRepository.QueryModulesDirectoryPath();

    /// <summary>
    /// 查詢已安裝模組清單。
    /// </summary>
    public IReadOnlyList<ModulesInstalledItemModel> QueryModules()
        => modulesRepository.QueryModules()
            .Select(QueryInstalledItem)
            .OrderBy(static item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// 匯入模組。
    /// </summary>
    public ModulesInstalledItemModel ImportModule(string sourceDirectoryPath)
        => QueryInstalledItem(modulesRepository.ImportModule(sourceDirectoryPath));

    /// <summary>
    /// 停用模組。
    /// </summary>
    public void DisableModule(string moduleId)
        => modulesRepository.DisableModule(moduleId);

    /// <summary>
    /// 解除安裝模組（刪除安裝資料夾）。
    /// </summary>
    public void UninstallModule(string installedFolderPath)
        => modulesRepository.DeleteModule(installedFolderPath);

    private static ModulesInstalledItemModel QueryInstalledItem(ModuleModel module)
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
            .FirstOrDefault(static path =>
            {
                var name = Path.GetFileName(path);
                return name.Equals("README.md", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("README.txt", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("README", StringComparison.OrdinalIgnoreCase);
            });

        return readmePath is null ? string.Empty : File.ReadAllText(readmePath, Encoding.UTF8);
    }
}
