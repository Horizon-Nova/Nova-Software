using System.Windows;
using Helix_Studio.Infrastructure.License;
using Helix_Studio.Infrastructure.Modules;
using Helix_Studio.Model.Modules;
using HorizonNova.HelixStudio.ModuleSdk;
using Microsoft.Extensions.Logging;

namespace Helix_Studio.Services;

/// <summary>外掛模組的掃描、載入內文啟用、授權檢查、健康狀態與卸載管理。</summary>
public sealed class ModuleRuntimeServices : IDisposable
{
    private readonly ModuleLoader  _loader;
    private readonly ILogger<ModuleRuntimeServices> _logger;
    private readonly WorkspaceServices _workspace;

    private readonly Dictionary<string, ShellContext>  _contexts     =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, ModuleEntry>   _activeEntries =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>當模組目錄掃描結果或已載入清單變更時，於 UI 執行緒上觸發。</summary>
    public event EventHandler? CatalogChanged;

    /// <summary>當單一模組回報執行健康度變化時，於 UI 執行緒上帶出模組識別與訊息。</summary>
    public event Action<string, ModuleHealth, string?>? ModuleHealthChanged;

    /// <summary>以日誌與工作區服務建立執行時期載入器。</summary>
    public ModuleRuntimeServices(
        ILogger<ModuleRuntimeServices> logger,
        WorkspaceServices workspace)
    {
        _logger   = logger;
        _workspace = workspace;
        _loader   = new ModuleLoader(
            logger as ILogger<ModuleLoader>
                ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ModuleLoader>.Instance);
    }

    /// <summary>取得目前已掃描到的模組清冊清單。</summary>
    public IReadOnlyList<ModuleManifest> GetManifests() => _loader.Manifests;

    /// <summary>在指定目錄掃描模組成品並觸發 <see cref="CatalogChanged"/>。</summary>
    public void Scan(string modulesDirectory)
    {
        _loader.Scan(modulesDirectory);

        var currentIds = new HashSet<string>(
            _loader.Manifests.Select(m => m.Id),
            StringComparer.OrdinalIgnoreCase);

        foreach (var id in _activeEntries.Keys.Except(currentIds).ToList())
        {
            _activeEntries.Remove(id);
            _contexts.Remove(id);
        }

        RaiseCatalogChanged();
    }

    /// <summary>模組設定儲存區中，授權金鑰字串的內部鍵名。</summary>
    internal const string LicenseKeySettingsKey = "_license_key";

    /// <summary>啟用指定模組；必要時驗證儲存的金鑰並回傳檢視或錯誤狀態。</summary>
    public ShellModuleActivationOutcome Activate(string moduleId)
    {
        try
        {
            if (_activeEntries.TryGetValue(moduleId, out var existing))
            {
                existing.EnsureActivated(GetOrCreateContext(moduleId));
                return ShellModuleActivationOutcome.Ok(existing.View!);
            }

            var manifest = _loader.Manifests
                .FirstOrDefault(m => string.Equals(m.Id, moduleId, StringComparison.OrdinalIgnoreCase));

            if (manifest?.RequiresLicense == true)
            {
                var storedKey = GetOrCreateContext(moduleId).Settings
                    .QueryString(LicenseKeySettingsKey);
                var licResult = ModuleLicenseVerifier.Verify(moduleId, storedKey);
                if (!licResult.IsValid)
                {
                    _logger.LogWarning(
                        "Module {Id} blocked — license check failed: {Reason}",
                        moduleId, licResult.ErrorMessage);
                    return ShellModuleActivationOutcome.KeyRequired(licResult);
                }
            }

            var context = GetOrCreateContext(moduleId);
            var entry   = _loader.Activate(moduleId, context);
            if (entry is null) return ShellModuleActivationOutcome.Failed();

            _activeEntries[moduleId] = entry;
            return ShellModuleActivationOutcome.Ok(entry.View!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate module {Id}", moduleId);
            return ShellModuleActivationOutcome.Failed();
        }
    }

    /// <summary>將模組的授權金鑰寫入該模組的設定儲存區。</summary>
    public void SaveModuleLicenseKey(string moduleId, string key)
        => GetOrCreateContext(moduleId).Settings.SaveString(LicenseKeySettingsKey, key);

    /// <summary>停止指定模組的執行實例（不卸除組件目錄）。</summary>
    public void Deactivate(string moduleId)
    {
        if (_activeEntries.TryGetValue(moduleId, out var entry))
            entry.Deactivate();
    }

    /// <summary>判斷已啟用的模組是否提供設定 UI。</summary>
    public bool ModuleHasSettings(string moduleId)
        => _activeEntries.TryGetValue(moduleId, out var entry) && entry.HasSettings;

    /// <summary>顯示模組設定前呼叫，以取得或建立模組的設定內容物件。</summary>
    public object? ActivateSettings(string moduleId)
    {
        if (!_activeEntries.TryGetValue(moduleId, out var entry)) return null;
        return entry.EnsureSettingsActivated(GetOrCreateContext(moduleId));
    }

    /// <summary>卸載模組組件、清除執行內文，若指定則非同步刪除安裝目錄。</summary>
    public void Unload(string moduleId, bool deleteFolder = false)
    {
        var alcWeakRef = deleteFolder ? _loader.GetEntryContextWeakRef(moduleId) : null;

        string? folderToDelete = null;
        if (deleteFolder)
        {
            var manifest = _loader.Manifests
                .FirstOrDefault(m =>
                    string.Equals(m.Id, moduleId, StringComparison.OrdinalIgnoreCase));

            folderToDelete = manifest?.InstalledFolderPath;

            if (string.IsNullOrEmpty(folderToDelete) &&
                _activeEntries.TryGetValue(moduleId, out var ae))
            {
                folderToDelete = System.IO.Path.GetDirectoryName(ae.Manifest.DllPath);
            }
        }

        if (_activeEntries.TryGetValue(moduleId, out var entry))
        {
            entry.Deactivate();
            _activeEntries.Remove(moduleId);
        }

        _contexts.Remove(moduleId);
        _loader.Unload(moduleId);

        RaiseCatalogChanged();

        if (!deleteFolder || string.IsNullOrEmpty(folderToDelete) ||
            !System.IO.Directory.Exists(folderToDelete))
            return;

        var folder  = folderToDelete;
        var weakRef = alcWeakRef;
        var logger  = _logger;

        Task.Run(async () =>
        {
            if (weakRef is not null)
            {
                for (int i = 0; i < 10 && weakRef.IsAlive; i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    if (weakRef.IsAlive)
                        await Task.Delay(100);
                }
            }

            for (int attempt = 1; attempt <= 10; attempt++)
            {
                try
                {
                    System.IO.Directory.Delete(folder, recursive: true);
                    logger.LogInformation(
                        "Module folder deleted (attempt {N}): {F}", attempt, folder);
                    return;
                }
                catch (Exception ex) when (attempt < 10)
                {
                    logger.LogDebug(ex, "Delete attempt {N} failed, retrying…", attempt);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    await Task.Delay(200 * attempt);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex, "Could not delete module folder after 10 attempts: {F}", folder);
                    return;
                }
            }
        });
    }

    /// <summary>卸載模組以準備替換安裝，回傳載入內文弱參考供呼叫端在刪除前等待回收。</summary>
    public WeakReference? UnloadForReplace(string moduleId)
    {
        var weakRef = _loader.GetEntryContextWeakRef(moduleId);
        Unload(moduleId, deleteFolder: false);
        return weakRef;
    }

    /// <summary>是否已有該模組的啟用執行個體（仍在 <c>_activeEntries</c> 中）。</summary>
    public bool IsModuleActivated(string moduleId)
        => _activeEntries.ContainsKey(moduleId);


    private ShellContext GetOrCreateContext(string moduleId)
    {
        if (!_contexts.TryGetValue(moduleId, out var ctx))
        {
            ctx = new ShellContext(
                moduleId,
                serviceType => null,
                () => _workspace.GetActive()?.Name ?? string.Empty);

            ctx.HealthChanged += (health, message) =>
                RaiseModuleHealthChanged(moduleId, health, message);

            _contexts[moduleId] = ctx;
        }
        return ctx;
    }

    private void RaiseModuleHealthChanged(string moduleId, ModuleHealth health, string? message)
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
            dispatcher.BeginInvoke(() => ModuleHealthChanged?.Invoke(moduleId, health, message));
        else
            ModuleHealthChanged?.Invoke(moduleId, health, message);
    }

    private void RaiseCatalogChanged()
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
            dispatcher.BeginInvoke(() => CatalogChanged?.Invoke(this, EventArgs.Empty));
        else
            CatalogChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>釋放模組載入器並清除所有作用中模組與內文快取。</summary>
    public void Dispose()
    {
        _loader.Dispose();
        _activeEntries.Clear();
        _contexts.Clear();
    }
}
