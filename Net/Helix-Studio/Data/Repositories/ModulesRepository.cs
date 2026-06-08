using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Helix_Studio.Infrastructure.Modules;
using Helix_Studio.Model;

namespace Helix_Studio.Data.Repositories;

/// <summary>
/// 模組資料夾與模組狀態的資料來源。
/// </summary>
public sealed class ModulesRepository : IDisposable
{
    private const string MainSection = "Main";
    private const string ModulesFolderName = "Modules";
    private const string ModuleStateDirectoryName = ".module-state";
    private const string ModulesStateFileName = "modules-state.json";

    private readonly IniRepository _iniRepository;
    private readonly WorkspacesRepository _workspacesRepository;
    private readonly object _stateSync = new();
    private FileSystemWatcher? _watcher;

    /// <summary>
    /// 模組資料異動事件。
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// 建立模組 Repository。
    /// </summary>
    public ModulesRepository(IniRepository iniRepository, WorkspacesRepository workspacesRepository)
    {
        _iniRepository = iniRepository;
        _workspacesRepository = workspacesRepository;
        _iniRepository.Changed += OnIniChanged;
        AttachWatcher();
    }

    /// <summary>
    /// 取得目前工作區 Modules 路徑。
    /// </summary>
    public string QueryModulesDirectoryPath()
    {
        var workspaceName = ResolveActiveWorkspaceName();
        if (string.IsNullOrWhiteSpace(workspaceName))
        {
            return Path.Combine(_workspacesRepository.QueryWorkspacesDirectoryPath(), ModulesFolderName);
        }

        var modulesPath = Path.Combine(
            _workspacesRepository.QueryWorkspacesDirectoryPath(),
            workspaceName,
            ModulesFolderName);

        Directory.CreateDirectory(modulesPath);
        return modulesPath;
    }

    /// <summary>
    /// 取得目前工作區中已啟用的模組。
    /// </summary>
    public IReadOnlyList<ModuleModel> QueryModules()
    {
        var installedModules = QueryInstalledModules();
        return installedModules
            .Where(static module => module.IsEnabled)
            .OrderBy(static module => module.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 取得目前工作區中所有已安裝模組，包含已停用項目。
    /// </summary>
    public IReadOnlyList<ModuleModel> QueryInstalledModules()
    {
        var disabledModuleIds = QueryDisabledModuleIds();
        return Directory.EnumerateDirectories(QueryModulesDirectoryPath())
            .Select(QueryModule)
            .Where(static module => module is not null)
            .Cast<ModuleModel>()
            .Select(module => module with
            {
                IsEnabled = !disabledModuleIds.Contains(module.Id),
            })
            .OrderBy(static module => module.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 匯入模組到目前工作區。
    /// </summary>
    public ModuleModel ImportModule(string sourceDirectoryPath)
    {
        var module = QueryModule(sourceDirectoryPath)
            ?? throw new InvalidOperationException("來源資料夾中找不到可辨識的模組 DLL。");

        var folderName = Path.GetFileName(sourceDirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var targetDirectoryPath = Path.Combine(QueryModulesDirectoryPath(), folderName);
        CopyDirectory(sourceDirectoryPath, targetDirectoryPath);
        SetModuleEnabledState(module.Id, isEnabled: true);
        Changed?.Invoke(this, EventArgs.Empty);

        return QueryModule(targetDirectoryPath) with
        {
            IsEnabled = true,
        } ?? throw new InvalidOperationException("模組複製完成後仍無法重新辨識安裝內容。");
    }

    /// <summary>
    /// 移除模組資料夾。
    /// </summary>
    public void DeleteModule(string moduleDirectoryPath)
    {
        var module = QueryModule(moduleDirectoryPath);
        Directory.Delete(moduleDirectoryPath, recursive: true);

        if (module is not null)
        {
            RemoveModuleState(module.Id);
            DeleteModuleStateDirectory(module.Id);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 將模組標示為停用，但保留安裝資料夾。
    /// </summary>
    public void DisableModule(string moduleId)
    {
        SetModuleEnabledState(moduleId, isEnabled: false);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 將模組標示為啟用。
    /// </summary>
    public void EnableModule(string moduleId)
    {
        SetModuleEnabledState(moduleId, isEnabled: true);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 釋放 watcher。
    /// </summary>
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

    private string ResolveActiveWorkspaceName()
    {
        var workspaceName = _iniRepository.QuerySnapshot<MainModel>(MainSection).Workspace ?? string.Empty;
        return _workspacesRepository.ResolveWorkspaceNameOrFallback(workspaceName);
    }

    private string QueryModulesStateFilePath()
    {
        var workspaceName = ResolveActiveWorkspaceName();
        if (string.IsNullOrWhiteSpace(workspaceName))
        {
            return Path.Combine(_workspacesRepository.QueryWorkspacesDirectoryPath(), ModulesStateFileName);
        }

        var workspaceDirectoryPath = _workspacesRepository.QueryWorkspaceDirectoryPath(workspaceName);
        var moduleStateDirectoryPath = Path.Combine(workspaceDirectoryPath, ModuleStateDirectoryName);
        Directory.CreateDirectory(moduleStateDirectoryPath);
        return Path.Combine(moduleStateDirectoryPath, ModulesStateFileName);
    }

    private HashSet<string> QueryDisabledModuleIds()
    {
        lock (_stateSync)
        {
            var state = QueryStateDocument();
            return new HashSet<string>(state.DisabledModuleIds, StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SetModuleEnabledState(string moduleId, bool isEnabled)
    {
        lock (_stateSync)
        {
            var state = QueryStateDocument();
            if (isEnabled)
            {
                state.DisabledModuleIds.RemoveAll(id => string.Equals(id, moduleId, StringComparison.OrdinalIgnoreCase));
            }
            else if (!state.DisabledModuleIds.Any(id => string.Equals(id, moduleId, StringComparison.OrdinalIgnoreCase)))
            {
                state.DisabledModuleIds.Add(moduleId);
            }

            PersistStateDocument(state);
        }
    }

    private void RemoveModuleState(string moduleId)
    {
        lock (_stateSync)
        {
            var state = QueryStateDocument();
            var removed = state.DisabledModuleIds.RemoveAll(id => string.Equals(id, moduleId, StringComparison.OrdinalIgnoreCase));
            if (removed > 0 || File.Exists(QueryModulesStateFilePath()))
            {
                PersistStateDocument(state);
            }
        }
    }

    private void DeleteModuleStateDirectory(string moduleId)
    {
        var workspaceName = ResolveActiveWorkspaceName();
        if (string.IsNullOrWhiteSpace(workspaceName))
        {
            return;
        }

        var workspaceDirectoryPath = _workspacesRepository.QueryWorkspaceDirectoryPath(workspaceName);
        var moduleStateDirectoryPath = Path.Combine(workspaceDirectoryPath, ModuleStateDirectoryName, moduleId);
        if (Directory.Exists(moduleStateDirectoryPath))
        {
            Directory.Delete(moduleStateDirectoryPath, recursive: true);
        }
    }

    private ModulesStateDocument QueryStateDocument()
    {
        var stateFilePath = QueryModulesStateFilePath();
        if (!File.Exists(stateFilePath))
        {
            return new ModulesStateDocument();
        }

        try
        {
            var json = File.ReadAllText(stateFilePath);
            return JsonSerializer.Deserialize<ModulesStateDocument>(json) ?? new ModulesStateDocument();
        }
        catch
        {
            return new ModulesStateDocument();
        }
    }

    private void PersistStateDocument(ModulesStateDocument state)
    {
        var stateFilePath = QueryModulesStateFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(stateFilePath)!);
        var normalized = state.DisabledModuleIds
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        File.WriteAllText(
            stateFilePath,
            JsonSerializer.Serialize(new ModulesStateDocument
            {
                DisabledModuleIds = normalized,
            }, new JsonSerializerOptions
            {
                WriteIndented = true,
            }));
    }

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
            var metadata = ModuleMetadataReader.ReadFromAssembly(dllPath, moduleDirectoryPath);
            return new ModuleModel
            {
                Id = metadata.Id,
                DisplayName = metadata.DisplayName,
                Version = metadata.Version,
                Author = metadata.Author,
                Description = metadata.Description,
                MinHostVersion = metadata.MinHostVersion,
                FolderPath = metadata.FolderPath,
                DllPath = metadata.DllPath,
                RequiresLicense = metadata.RequiresLicense,
                IsLoadable = metadata.IsLoadable,
                IsEnabled = true,
                HasSettings = metadata.HasSettings,
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
                IsLoadable = false,
                IsEnabled = true,
                HasSettings = false,
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

    private sealed class ModulesStateDocument
    {
        public List<string> DisabledModuleIds { get; init; } = [];
    }
}
