using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Helix_Studio.Model;
using Helix_Studio.Utils;

namespace Helix_Studio.Data.Repositories;

/// <summary>
/// 模組資料存取（以資料夾為單位）。
/// </summary>
public sealed class ModulesRepository : IDisposable
{
    private const string MainSection = "Main";
    private const string ModulesFolderName = "Modules";

    private readonly IniRepository _iniRepository;
    private FileSystemWatcher? _watcher;

    /// <summary>
    /// 模組清單變更事件。
    /// </summary>
    public event EventHandler? Changed;

    public ModulesRepository(IniRepository iniRepository)
    {
        _iniRepository = iniRepository;
        _iniRepository.Changed += OnIniChanged;
        AttachWatcher();
    }

    /// <summary>
    /// 取得目前工作區的模組目錄。
    /// </summary>
    public string QueryModulesDirectoryPath()
    {
        var workspaceName = _iniRepository.QuerySnapshot<MainModel>(MainSection).Workspace ?? string.Empty;
        var modulesPath = Path.Combine(
            AppPathHelper.QueryWorkspacesDirectory(),
            workspaceName,
            ModulesFolderName);

        Directory.CreateDirectory(modulesPath);
        return modulesPath;
    }

    /// <summary>
    /// 取得模組清單。
    /// </summary>
    public IReadOnlyList<ModuleModel> QueryModules()
        => Directory.EnumerateDirectories(QueryModulesDirectoryPath())
            .Select(QueryModule)
            .Where(static module => module is not null)
            .Cast<ModuleModel>()
            .OrderBy(static module => module.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// 匯入模組（將資料夾複製到目前工作區 Modules 目錄）。
    /// </summary>
    public ModuleModel ImportModule(string sourceDirectoryPath)
    {
        var module = QueryModule(sourceDirectoryPath)
            ?? throw new InvalidOperationException("來源資料夾找不到任何 DLL，無法辨識模組。");

        var folderName = Path.GetFileName(sourceDirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var targetDirectoryPath = Path.Combine(QueryModulesDirectoryPath(), folderName);
        CopyDirectory(sourceDirectoryPath, targetDirectoryPath);
        Changed?.Invoke(this, EventArgs.Empty);

        return QueryModule(targetDirectoryPath)
            ?? throw new InvalidOperationException("已完成複製，但仍無法從目標資料夾辨識模組。");
    }

    /// <summary>
    /// 刪除模組資料夾。
    /// </summary>
    public void DeleteModule(string moduleDirectoryPath)
    {
        Directory.Delete(moduleDirectoryPath, recursive: true);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 停用模組（目前僅觸發變更事件，實作留待後續規格確認）。
    /// </summary>
    public void DisableModule(string moduleId)
        => Changed?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _iniRepository.Changed -= OnIniChanged;
        _watcher?.Dispose();
        _watcher = null;
    }

    private void OnIniChanged(object? sender, EventArgs e) => AttachWatcher();

    private void AttachWatcher()
    {
        _watcher?.Dispose();

        var modulesDirectoryPath = QueryModulesDirectoryPath();
        _watcher = new FileSystemWatcher(modulesDirectoryPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };

        _watcher.Created += OnModulesChanged;
        _watcher.Deleted += OnModulesChanged;
        _watcher.Changed += OnModulesChanged;
        _watcher.Renamed += OnModulesChanged;
    }

    private void OnModulesChanged(object? sender, FileSystemEventArgs e)
        => Changed?.Invoke(this, EventArgs.Empty);

    private static ModuleModel? QueryModule(string moduleDirectoryPath)
    {
        var dllPath = Directory.EnumerateFiles(moduleDirectoryPath, "*.dll", SearchOption.AllDirectories)
            .FirstOrDefault();

        return dllPath is null ? null : QueryModuleFromAssembly(dllPath, moduleDirectoryPath);
    }

    private static ModuleModel QueryModuleFromAssembly(string dllPath, string moduleDirectoryPath)
    {
        try
        {
            var assemblyName = AssemblyName.GetAssemblyName(dllPath);
            var id = assemblyName.Name ?? Path.GetFileNameWithoutExtension(dllPath);
            var version = assemblyName.Version?.ToString() ?? "0.0.0";
            return new ModuleModel
            {
                Id = id,
                DisplayName = id,
                Version = version,
                FolderPath = moduleDirectoryPath,
                DllPath = dllPath,
            };
        }
        catch (Exception)
        {
            var fallbackId = Path.GetFileNameWithoutExtension(dllPath);
            return new ModuleModel
            {
                Id = fallbackId,
                DisplayName = fallbackId,
                Version = "0.0.0",
                FolderPath = moduleDirectoryPath,
                DllPath = dllPath,
            };
        }
    }

    private static void CopyDirectory(string sourceDirectoryPath, string targetDirectoryPath)
    {
        Directory.CreateDirectory(targetDirectoryPath);

        foreach (var filePath in Directory.EnumerateFiles(sourceDirectoryPath))
        {
            var targetFilePath = Path.Combine(targetDirectoryPath, Path.GetFileName(filePath));
            File.Copy(filePath, targetFilePath, overwrite: true);
        }

        foreach (var childDirectoryPath in Directory.EnumerateDirectories(sourceDirectoryPath))
        {
            var targetChildDirectoryPath = Path.Combine(targetDirectoryPath, Path.GetFileName(childDirectoryPath));
            CopyDirectory(childDirectoryPath, targetChildDirectoryPath);
        }
    }
}
