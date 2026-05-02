using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;
using HorizonNova.HelixStudio.ModuleSdk;

namespace Helix_Studio.Infrastructure.Modules;

/// <summary>單一模組之載入內容、執行個體與檢視快取。</summary>
public sealed class ModuleEntry : IDisposable
{
    private readonly ModuleLoadContext    _context;
    private          IModule?             _instance;
    private          FrameworkElement?    _view;
    private          FrameworkElement?    _settingsView;

    /// <summary>模組 manifest（識別、路徑、圖示等）。</summary>
    public ModuleManifest Manifest { get; }

    /// <summary>是否已完成啟用並持有 <see cref="IModule"/> 執行個體。</summary>
    public bool IsActivated => _instance is not null;

    /// <summary>模組是否實作設定檢視。</summary>
    public bool HasSettings => _instance is IModuleWithSettings;

    /// <summary>主功能檢視元素（啟用後快取）。</summary>
    public FrameworkElement? View         => _view;

    /// <summary>模組設定檢視元素（延遲建立）。</summary>
    public FrameworkElement? SettingsView => _settingsView;

    internal ModuleEntry(string folder, ModuleManifest manifest)
    {
        Manifest = manifest;
        _context = new ModuleLoadContext(folder);
    }

    internal void EnsureActivated(IShellContext context)
    {
        if (_instance is null)
        {
            var asm  = _context.LoadFromAssemblyPath(Manifest.DllPath);
            var type = asm.GetTypes()
                .FirstOrDefault(t =>
                    !t.IsAbstract &&
                    !t.IsInterface &&
                    typeof(IModule).IsAssignableFrom(t))
                ?? throw new InvalidOperationException(
                       $"No concrete IModule implementation found in {Path.GetFileName(Manifest.DllPath)}");

            _instance = (IModule)Activator.CreateInstance(type)!;
            _view     = _instance.CreateView(context);
        }

        _instance.OnActivated(context);
    }

    internal FrameworkElement? EnsureSettingsActivated(IShellContext context)
    {
        if (_instance is not IModuleWithSettings withSettings) return null;
        if (_settingsView is null)
            _settingsView = withSettings.CreateSettingsView(context);
        return _settingsView;
    }

    /// <summary>呼叫模組之停用回呼（不卸載組件）。</summary>
    public void Deactivate() => _instance?.OnDeactivated();

    internal WeakReference GetLoadContextWeakRef() => new(_context);

    /// <summary>釋放模組執行個體並卸載模組之 <see cref="ModuleLoadContext"/>。</summary>
    public void Dispose()
    {
        try { _instance?.Dispose(); } catch { }
        _instance     = null;
        _view         = null;
        _settingsView = null;
        _context.Unload();
    }
}
