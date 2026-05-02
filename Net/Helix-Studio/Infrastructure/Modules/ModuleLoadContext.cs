using System.Reflection;
using System.Runtime.Loader;

namespace Helix_Studio.Infrastructure.Modules;

/// <summary>可卸載之模組組件載入內容；SDK 組件改由預設內容載入。</summary>
internal sealed class ModuleLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    /// <summary>以模組資料夾路徑建立可收集之載入內容。</summary>
    public ModuleLoadContext(string moduleFolder)
        : base(name: System.IO.Path.GetFileName(moduleFolder), isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(moduleFolder);
    }

    /// <inheritdoc />
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is "Helix-Studio.ModuleSdk")
            return null;

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is not null ? LoadFromAssemblyPath(path) : null;
    }

    /// <inheritdoc />
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is not null ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
    }
}
