using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;
using Helix_Studio.Model;
using HorizonNova.HelixStudio.Contracts;

namespace Helix_Studio.Core;

/// <summary>
/// 模組核心，負責監看目前工作區的模組資料夾並提供模組查詢、載入與卸載能力。
/// </summary>
public sealed class ModulesCore : IDisposable
{
    private const string MainSection = "Main";
    private const string ModulesFolderName = "Modules";
    private readonly IniCore _iniCore;
    private readonly LicenseCore _licenseCore;
    private readonly Dictionary<string, ModuleRuntime> _runtimes = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _watcher;

    /// <summary>
    /// 模組資料夾發生 I/O 變更時觸發。
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// 建立模組核心。
    /// </summary>
    /// <param name="iniCore">INI 核心。</param>
    /// <param name="licenseCore">授權核心。</param>
    public ModulesCore(IniCore iniCore, LicenseCore licenseCore)
    {
        _iniCore = iniCore;
        _licenseCore = licenseCore;
        _iniCore.Changed += OnIniChanged;
        AttachWatcher();
    }

    /// <summary>
    /// 查詢目前工作區的模組資料夾路徑。
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
    /// 查詢目前工作區的模組清單。
    /// </summary>
    public IReadOnlyList<ModuleModel> QueryModules()
        => Directory.EnumerateDirectories(QueryModulesDirectoryPath())
            .Select(QueryModule)
            .Where(static module => module is not null)
            .Cast<ModuleModel>()
            .OrderBy(static module => module.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// 查詢指定目錄中的模組資訊。
    /// </summary>
    /// <param name="moduleDirectoryPath">模組目錄路徑。</param>
    public ModuleModel? QueryModule(string moduleDirectoryPath)
    {
        var dllPath = Directory.EnumerateFiles(moduleDirectoryPath, "*.dll", SearchOption.AllDirectories)
            .FirstOrDefault(TryHasModuleIdentity);

        return dllPath is null ? null : TryReadModule(dllPath, moduleDirectoryPath);
    }

    /// <summary>
    /// 匯入指定來源資料夾中的模組。
    /// </summary>
    /// <param name="sourceDirectoryPath">來源模組資料夾。</param>
    /// <param name="license">模組授權資料。</param>
    public ModuleModel ImportModule(string sourceDirectoryPath, LicenseModel? license = null)
    {
        var module = QueryModule(sourceDirectoryPath)
            ?? throw new InvalidOperationException("來源資料夾不包含有效模組。");

        if (module.RequiresLicense && (license is null || !_licenseCore.ValidateModuleLicense(module.Id, license)))
        {
            throw new InvalidOperationException("模組授權驗證失敗。");
        }

        var folderName = Path.GetFileName(sourceDirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var targetDirectoryPath = Path.Combine(QueryModulesDirectoryPath(), folderName);
        CopyDirectory(sourceDirectoryPath, targetDirectoryPath);
        Changed?.Invoke(this, EventArgs.Empty);

        return QueryModule(targetDirectoryPath)
            ?? throw new InvalidOperationException("模組匯入完成後無法重新讀取。");
    }

    /// <summary>
    /// 刪除指定模組資料夾。
    /// </summary>
    /// <param name="moduleDirectoryPath">模組資料夾路徑。</param>
    public void DeleteModule(string moduleDirectoryPath)
    {
        var module = QueryModule(moduleDirectoryPath);
        if (module is not null)
        {
            DisableModule(module.Id);
        }

        Directory.Delete(moduleDirectoryPath, true);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 對所有已載入執行個體呼叫 <see cref="IModule.OnDeactivated"/>（不卸載組件）。
    /// </summary>
    public void DeactivateAllModuleUi()
    {
        foreach (var runtime in _runtimes.Values)
        {
            runtime.DeactivateUi();
        }
    }

    /// <summary>
    /// 載入指定模組並建立 View。
    /// </summary>
    /// <param name="moduleId">模組識別碼。</param>
    /// <param name="shellContext">宿主內容。</param>
    public FrameworkElement LoadModule(string moduleId, IShellContext shellContext)
    {
        var module = QueryModules().FirstOrDefault(model => string.Equals(model.Id, moduleId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("找不到指定模組。");

        if (!_runtimes.TryGetValue(module.Id, out var runtime))
        {
            runtime = new ModuleRuntime(module);
            _runtimes[module.Id] = runtime;
        }

        return runtime.Load(shellContext);
    }

    /// <summary>
    /// 停用指定模組並卸載執行內容。
    /// </summary>
    /// <param name="moduleId">模組識別碼。</param>
    public void DisableModule(string moduleId)
    {
        if (_runtimes.Remove(moduleId, out var runtime))
        {
            runtime.Dispose();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 釋放模組核心與檔案監看器。
    /// </summary>
    public void Dispose()
    {
        _iniCore.Changed -= OnIniChanged;
        _watcher?.Dispose();
        _watcher = null;

        foreach (var runtime in _runtimes.Values)
        {
            runtime.Dispose();
        }

        _runtimes.Clear();
    }

    private void OnIniChanged(object? sender, EventArgs e)
    {
        AttachWatcher();
    }

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

    private static bool TryHasModuleIdentity(string dllPath)
        => TryReadModule(dllPath, Path.GetDirectoryName(dllPath) ?? string.Empty) is not null;

    private static ModuleModel? TryReadModule(string dllPath, string moduleDirectoryPath)
    {
        var runtimePaths = Directory.GetFiles(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
        var folderDllPaths = Directory.GetFiles(Path.GetDirectoryName(dllPath)!, "*.dll", SearchOption.TopDirectoryOnly);
        var appDllPaths = Directory.GetFiles(AppContext.BaseDirectory, "*.dll", SearchOption.TopDirectoryOnly);
        var resolver = new PathAssemblyResolver(runtimePaths.Concat(folderDllPaths).Concat(appDllPaths));
        using var metadataLoadContext = new MetadataLoadContext(resolver);

        Assembly assembly;
        try
        {
            assembly = metadataLoadContext.LoadFromAssemblyPath(dllPath);
        }
        catch
        {
            return null;
        }

        var moduleId = QueryAttributeValue(assembly, nameof(ModuleIdAttribute), nameof(ModuleIdAttribute.Id));
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return null;
        }

        return new ModuleModel
        {
            Id = moduleId,
            DisplayName = QueryAttributeValue(assembly, nameof(ModuleDisplayNameAttribute), nameof(ModuleDisplayNameAttribute.DisplayName)) ?? moduleId,
            Version = QueryAttributeValue(assembly, nameof(ModuleVersionAttribute), nameof(ModuleVersionAttribute.Version)) ?? "0.0.0",
            Author = QueryAttributeValue(assembly, nameof(ModuleAuthorAttribute), nameof(ModuleAuthorAttribute.Author)) ?? string.Empty,
            Description = QueryAttributeValue(assembly, nameof(ModuleDescriptionAttribute), nameof(ModuleDescriptionAttribute.Description)) ?? string.Empty,
            MinHostVersion = QueryAttributeValue(assembly, nameof(ModuleMinHostVersionAttribute), nameof(ModuleMinHostVersionAttribute.MinVersion)) ?? "1.0.0",
            FolderPath = moduleDirectoryPath,
            DllPath = dllPath,
            RequiresLicense = assembly.GetCustomAttributesData()
                .Any(attribute => attribute.AttributeType.Name == nameof(ModuleRequiresLicenseAttribute)),
        };
    }

    private static string? QueryAttributeValue(Assembly assembly, string attributeName, string propertyName)
    {
        var attribute = assembly.GetCustomAttributesData()
            .FirstOrDefault(data => data.AttributeType.Name == attributeName);

        if (attribute is null)
        {
            return null;
        }

        if (attribute.ConstructorArguments.Count > 0)
        {
            return attribute.ConstructorArguments[0].Value?.ToString();
        }

        return attribute.NamedArguments
            .FirstOrDefault(argument => string.Equals(argument.MemberName, propertyName, StringComparison.Ordinal))
            .TypedValue.Value?.ToString();
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

    private sealed class ModuleRuntime : IDisposable
    {
        private readonly ModuleModel _module;
        private readonly ModuleLoadContext _loadContext;
        private IModule? _instance;
        private FrameworkElement? _view;

        public ModuleRuntime(ModuleModel module)
        {
            _module = module;
            _loadContext = new ModuleLoadContext(module.FolderPath);
        }

        public void DeactivateUi() => _instance?.OnDeactivated();

        public FrameworkElement Load(IShellContext shellContext)
        {
            if (_instance is null)
            {
                var assembly = _loadContext.LoadFromAssemblyPath(_module.DllPath);
                var type = assembly.GetTypes()
                    .FirstOrDefault(type =>
                        !type.IsAbstract &&
                        !type.IsInterface &&
                        typeof(IModule).IsAssignableFrom(type))
                    ?? throw new InvalidOperationException("找不到 IModule 實作。");

                _instance = (IModule)Activator.CreateInstance(type)!;
                _view = _instance.CreateView(shellContext);
            }

            _instance.OnActivated(shellContext);
            return _view!;
        }

        public void Dispose()
        {
            try
            {
                _instance?.OnDeactivated();
                _instance?.Dispose();
            }
            finally
            {
                _instance = null;
                _view = null;
                _loadContext.Unload();
            }
        }
    }

    private sealed class ModuleLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public ModuleLoadContext(string moduleDirectoryPath)
            : base(Path.GetFileName(moduleDirectoryPath), isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(moduleDirectoryPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name is "Helix-Studio.Contracts")
            {
                return null;
            }

            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            return assemblyPath is null ? null : LoadFromAssemblyPath(assemblyPath);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return libraryPath is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(libraryPath);
        }
    }
}
