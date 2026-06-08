using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Helix_Studio.Data.Repositories;
using Helix_Studio.Model;

namespace Helix_Studio.Domain;

/// <summary>
/// Modules 頁與模組狀態的領域邏輯。
/// </summary>
public sealed class ModulesDomain(
    ModulesRepository modulesRepository,
    IniRepository iniRepository)
{
    /// <summary>
    /// 模組或設定異動事件。
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
    /// 取得目前工作區 Modules 目錄。
    /// </summary>
    public string QueryModulesDirectoryPath()
        => modulesRepository.QueryModulesDirectoryPath();

    /// <summary>
    /// 取得目前工作區的所有已安裝模組。
    /// </summary>
    public IReadOnlyList<ModulesInstalledItemModel> QueryModules()
        => modulesRepository.QueryInstalledModules()
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
    /// 啟用模組。
    /// </summary>
    public void EnableModule(string moduleId)
        => modulesRepository.EnableModule(moduleId);

    /// <summary>
    /// 移除模組安裝資料夾。
    /// </summary>
    public void UninstallModule(string installedFolderPath)
        => modulesRepository.DeleteModule(installedFolderPath);

    private static ModulesInstalledItemModel QueryInstalledItem(ModuleModel module)
    {
        var readmeText = QueryReadmeText(module.FolderPath);
        var statusText = !module.IsEnabled
            ? "已停用"
            : module.IsLoadable
                ? "可使用"
                : "不可用";

        return new ModulesInstalledItemModel
        {
            Id = module.Id,
            DisplayName = module.DisplayName,
            Version = module.Version,
            Author = module.Author,
            Description = module.Description,
            MinHostVersion = module.MinHostVersion,
            InstalledFolderPath = module.FolderPath,
            DllPath = module.DllPath,
            StatusText = statusText,
            Icon = null,
            ReadmeText = readmeText,
            IsLoadable = module.IsLoadable,
            IsEnabled = module.IsEnabled,
            HasSettings = module.HasSettings,
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
