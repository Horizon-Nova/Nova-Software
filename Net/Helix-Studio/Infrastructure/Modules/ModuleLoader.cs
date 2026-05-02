using System.IO;
using System.Reflection;
using HorizonNova.HelixStudio.ModuleSdk;
using Microsoft.Extensions.Logging;

namespace Helix_Studio.Infrastructure.Modules;

/// <summary>掃描模組目錄、讀取 manifest、載入與卸載模組組件。</summary>
public sealed class ModuleLoader : IDisposable
{
    private readonly ILogger<ModuleLoader> _logger;

    private readonly Dictionary<string, ModuleEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<ModuleManifest> _manifests = [];

    /// <summary>建立載入器並指定日誌。</summary>
    public ModuleLoader(ILogger<ModuleLoader> logger) => _logger = logger;

    /// <summary>最近一次掃描得到之 manifest 清單（唯讀）。</summary>
    public IReadOnlyList<ModuleManifest> Manifests => _manifests;

    /// <summary>掃描指定根目錄下之子資料夾，更新 manifest 與 <see cref="ModuleEntry"/> 快取。</summary>
    public void Scan(string modulesDirectory)
    {
        if (!Directory.Exists(modulesDirectory)) return;

        var found     = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manifests = new List<ModuleManifest>();

        foreach (var folder in Directory.EnumerateDirectories(modulesDirectory))
        {
            try
            {
                var manifest = TryScanFolder(folder);
                if (manifest is null) continue;

                found.Add(manifest.Id);
                manifests.Add(manifest);

                if (!_entries.ContainsKey(manifest.Id))
                    _entries[manifest.Id] = new ModuleEntry(folder, manifest);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to scan module folder: {Folder}", folder);
            }
        }

        foreach (var id in _entries.Keys.Except(found).ToList())
            UnloadInternal(id);

        _manifests = manifests.AsReadOnly();
    }

    /// <summary>啟用指定模組並回傳條目；失敗時回傳 null。</summary>
    public ModuleEntry? Activate(string moduleId, IShellContext context)
    {
        if (!_entries.TryGetValue(moduleId, out var entry)) return null;

        try
        {
            entry.EnsureActivated(context);
            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate module {Id}", moduleId);
            return null;
        }
    }

    /// <summary>卸載並移除指定模組條目。</summary>
    public void Unload(string moduleId) => UnloadInternal(moduleId);

    /// <summary>取得模組載入內容之弱參考（供記憶體回收觀察）。</summary>
    public WeakReference? GetEntryContextWeakRef(string moduleId)
        => _entries.TryGetValue(moduleId, out var e) ? e.GetLoadContextWeakRef() : null;


    private static ModuleManifest? TryScanFolder(string folder)
    {
        foreach (var dll in Directory.EnumerateFiles(folder, "*.dll", SearchOption.TopDirectoryOnly))
        {
            var manifest = TryReadManifestFromDll(dll);
            if (manifest is not null)
                return manifest with { InstalledFolderPath = folder };
        }
        return null;
    }

    private static ModuleManifest? TryReadManifestFromDll(string dllPath)
    {
        var runtimePaths = Directory.GetFiles(
            System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
            "*.dll");

        var folderDlls = Directory.GetFiles(
            Path.GetDirectoryName(dllPath)!, "*.dll", SearchOption.TopDirectoryOnly);
        var appDlls = Directory.GetFiles(
            AppContext.BaseDirectory, "*.dll", SearchOption.TopDirectoryOnly);

        var resolver  = new PathAssemblyResolver(runtimePaths.Concat(folderDlls).Concat(appDlls));
        using var mlc = new MetadataLoadContext(resolver);

        Assembly asm;
        try { asm = mlc.LoadFromAssemblyPath(dllPath); }
        catch { return null; }

        var moduleIdAttr = asm.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.Name == nameof(ModuleIdAttribute));
        if (moduleIdAttr is null) return null;

        string GetAttr(string typeName)
        {
            var attr = asm.GetCustomAttributesData()
                .FirstOrDefault(a => a.AttributeType.Name == typeName);
            return attr?.ConstructorArguments.Count > 0
                ? attr.ConstructorArguments[0].Value?.ToString() ?? string.Empty
                : string.Empty;
        }

        var id = moduleIdAttr.ConstructorArguments[0].Value?.ToString();
        if (string.IsNullOrWhiteSpace(id)) return null;

        var iconResourceName = GetAttr(nameof(ModuleIconResourceAttribute));
        var iconPngBytes = TryLoadEmbeddedIconPngBytes(dllPath, iconResourceName);

        bool requiresLicense = asm.GetCustomAttributesData()
            .Any(a => a.AttributeType.Name == nameof(ModuleRequiresLicenseAttribute));

        return new ModuleManifest
        {
            Id              = id,
            DisplayName     = GetAttr(nameof(ModuleDisplayNameAttribute)).Or(id),
            Version         = GetAttr(nameof(ModuleVersionAttribute)).Or("0.0.0"),
            Author          = GetAttr(nameof(ModuleAuthorAttribute)),
            Description     = GetAttr(nameof(ModuleDescriptionAttribute)),
            MinHostVersion  = GetAttr(nameof(ModuleMinHostVersionAttribute)).Or("1.0.0"),
            DllPath         = dllPath,
            IconPngBytes    = iconPngBytes,
            RequiresLicense = requiresLicense,
        };
    }

    private static byte[]? TryLoadEmbeddedIconPngBytes(string dllPath, string resourceName)
    {
        if (string.IsNullOrWhiteSpace(resourceName)) return null;
        try
        {
            var bytes = File.ReadAllBytes(dllPath);
            var asm   = Assembly.Load(bytes);
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream is null) return null;

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
        catch { return null; }
    }

    private void UnloadInternal(string moduleId)
    {
        if (!_entries.TryGetValue(moduleId, out var entry)) return;
        entry.Dispose();
        _entries.Remove(moduleId);

        _manifests = _manifests
            .Where(m => !string.Equals(m.Id, moduleId, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>卸載所有已掃描之模組並清空快取。</summary>
    public void Dispose()
    {
        foreach (var entry in _entries.Values) entry.Dispose();
        _entries.Clear();
    }
}

file static class StringExt
{
    /// <summary>字串為空白時回傳後援值。</summary>
    public static string Or(this string? s, string fallback)
        => string.IsNullOrWhiteSpace(s) ? fallback : s;
}
