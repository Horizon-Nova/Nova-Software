using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Helix_Studio.Model;

namespace Helix_Studio.Core;

/// <summary>
/// 模組 I/O 核心。
/// </summary>
public sealed class ModulesCore : IDisposable
{
    private const string MainSection = "Main";
    private const string ModulesFolderName = "Modules";
    private readonly IniCore _iniCore;
    private FileSystemWatcher? _watcher;

    /// <summary>
    /// 當模組目錄內容變更時觸發。
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// 建立模組核心並訂閱 INI 與檔案系統變更。
    /// </summary>
    /// <param name="iniCore">INI 核心。</param>
    public ModulesCore(IniCore iniCore)
    {
        _iniCore = iniCore;
        _iniCore.Changed += OnIniChanged;
        AttachWatcher();
    }

    /// <summary>
    /// 查詢目前工作區的模組目錄路徑。
    /// </summary>
    public string QueryModulesDirectoryPath()
    {
        var workspaceName = _iniCore.QuerySnapshot<MainModel>(MainSection).Workspace ?? string.Empty;
        var modulesPath = Path.Combine(
            AppPathCore.QueryWorkspacesDirectory(),
            workspaceName,
            ModulesFolderName);
        Directory.CreateDirectory(modulesPath);
        return modulesPath;
    }

    /// <summary>
    /// 查詢目前工作區已安裝的模組清單。
    /// </summary>
    public IReadOnlyList<ModuleModel> QueryModules()
        => Directory.EnumerateDirectories(QueryModulesDirectoryPath())
            .Select(QueryModule)
            .Where(static module => module is not null)
            .Cast<ModuleModel>()
            .OrderBy(static module => module.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// 查詢指定模組目錄的資訊。
    /// </summary>
    /// <param name="moduleDirectoryPath">模組目錄路徑。</param>
    public ModuleModel? QueryModule(string moduleDirectoryPath)
    {
        var dllPath = Directory.EnumerateFiles(moduleDirectoryPath, "*.dll", SearchOption.AllDirectories)
            .FirstOrDefault();

        if (dllPath is null)
        {
            return null;
        }

        return QueryModuleFromAssembly(dllPath, moduleDirectoryPath);
    }

    /// <summary>
    /// 匯入模組（複製整個模組資料夾至目前工作區）。
    /// </summary>
    /// <param name="sourceDirectoryPath">來源模組資料夾。</param>
    public ModuleModel ImportModule(string sourceDirectoryPath)
    {
        var module = QueryModule(sourceDirectoryPath)
            ?? throw new InvalidOperationException("來源模組資料夾未找到有效 DLL。");

        var folderName = Path.GetFileName(sourceDirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var targetDirectoryPath = Path.Combine(QueryModulesDirectoryPath(), folderName);
        CopyDirectory(sourceDirectoryPath, targetDirectoryPath);
        Changed?.Invoke(this, EventArgs.Empty);

        return QueryModule(targetDirectoryPath)
            ?? throw new InvalidOperationException("匯入後未能從目的資料夾解析模組資訊。");
    }

    /// <summary>
    /// 刪除指定模組資料夾。
    /// </summary>
    /// <param name="moduleDirectoryPath">模組資料夾。</param>
    public void DeleteModule(string moduleDirectoryPath)
    {
        Directory.Delete(moduleDirectoryPath, true);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 停用模組。
    /// </summary>
    /// <param name="moduleId">模組識別碼。</param>
    public void DisableModule(string moduleId)
        => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// 釋放資源。
    /// </summary>
    public void Dispose()
    {
        _iniCore.Changed -= OnIniChanged;
        _watcher?.Dispose();
        _watcher = null;
    }

    private void OnIniChanged(object? sender, EventArgs e)
        => AttachWatcher();

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
        _watcher.Renamed += OnModulesRenamed;
    }

    private void OnModulesChanged(object? sender, FileSystemEventArgs e)
        => Changed?.Invoke(this, EventArgs.Empty);

    private void OnModulesRenamed(object? sender, RenamedEventArgs e)
        => Changed?.Invoke(this, EventArgs.Empty);

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
                Author = string.Empty,
                Description = string.Empty,
                MinHostVersion = string.Empty,
                FolderPath = moduleDirectoryPath,
                DllPath = dllPath,
                RequiresLicense = false,
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
                Author = string.Empty,
                Description = string.Empty,
                MinHostVersion = string.Empty,
                FolderPath = moduleDirectoryPath,
                DllPath = dllPath,
                RequiresLicense = false,
            };
        }
    }

    private static void CopyDirectory(string sourceDirectoryPath, string targetDirectoryPath)
    {
        Directory.CreateDirectory(targetDirectoryPath);

        foreach (var filePath in Directory.EnumerateFiles(sourceDirectoryPath))
        {
            var targetFilePath = Path.Combine(targetDirectoryPath, Path.GetFileName(filePath));
            File.Copy(filePath, targetFilePath, true);
        }

        foreach (var childDirectoryPath in Directory.EnumerateDirectories(sourceDirectoryPath))
        {
            var targetChildDirectoryPath = Path.Combine(targetDirectoryPath, Path.GetFileName(childDirectoryPath));
            CopyDirectory(childDirectoryPath, targetChildDirectoryPath);
        }
    }
}

