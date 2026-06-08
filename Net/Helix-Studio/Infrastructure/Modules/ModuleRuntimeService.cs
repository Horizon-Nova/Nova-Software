using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using Helix_Studio.AppRuntime;
using Helix_Studio.Data.Repositories;
using Helix_Studio.Infrastructure.Licensing;
using Microsoft.Extensions.Logging;
using EventLogLevel = Helix_Studio.Model.EventLogLevel;
using HorizonNova.HelixStudio.ModuleSdk;

namespace Helix_Studio.Infrastructure.Modules;

internal sealed class ModuleRuntimeService(
    IniRepository iniRepository,
    WorkspacesRepository workspacesRepository,
    ModulesRepository modulesRepository,
    LicenseVerificationService licenseVerificationService,
    NotificationRuntime notificationRuntime,
    EventLogRepository eventLogRepository) : IDisposable
{
    private const string MainSection = "Main";
    private const string ModuleStateDirectoryName = ".module-state";
    private const string LegacyLicenseKeySettingsKey = "_license_key";
    private readonly Dictionary<string, LoadedModuleHandle> _loadedModules = new(StringComparer.OrdinalIgnoreCase);
    private string? _activeModuleCacheKey;

    public void Dispose()
    {
        DeactivateCurrentModule();

        foreach (var handle in _loadedModules.Values)
        {
            handle.Module.Dispose();
        }

        _loadedModules.Clear();
    }

    public FrameworkElement CreateModuleView(string moduleId)
    {
        var handle = QueryOrLoadModule(moduleId);
        handle.MainView ??= handle.Module.CreateView(handle.Context);
        return handle.MainView;
    }

    public FrameworkElement? CreateSettingsView(string moduleId)
    {
        var handle = QueryOrLoadModule(moduleId);
        if (handle.Module is not IModuleWithSettings moduleWithSettings)
        {
            return null;
        }

        handle.SettingsView ??= moduleWithSettings.CreateSettingsView(handle.Context);
        return handle.SettingsView;
    }

    public void ActivateModule(string moduleId)
    {
        var handle = QueryOrLoadModule(moduleId);
        if (string.Equals(_activeModuleCacheKey, handle.CacheKey, StringComparison.OrdinalIgnoreCase))
        {
            eventLogRepository.Record(EventLogLevel.Info, handle.ModuleId, "模組已啟動");
            handle.Module.OnActivated(handle.Context);
            return;
        }

        DeactivateCurrentModule();
        eventLogRepository.Record(EventLogLevel.Info, handle.ModuleId, "模組已啟動");
        handle.Module.OnActivated(handle.Context);
        _activeModuleCacheKey = handle.CacheKey;
    }

    public void DeactivateCurrentModule()
    {
        if (string.IsNullOrWhiteSpace(_activeModuleCacheKey))
        {
            return;
        }

        if (_loadedModules.TryGetValue(_activeModuleCacheKey, out var handle))
        {
            eventLogRepository.Record(EventLogLevel.Info, handle.ModuleId, "模組已停用");
            handle.Module.OnDeactivated();
        }

        _activeModuleCacheKey = null;
    }

    private LoadedModuleHandle QueryOrLoadModule(string moduleId)
    {
        var activeWorkspaceName = ResolveActiveWorkspaceName();
        var cacheKey = $"{activeWorkspaceName}|{moduleId}";
        if (_loadedModules.TryGetValue(cacheKey, out var existing))
        {
            return existing;
        }

        var moduleMetadata = modulesRepository.QueryModules()
            .FirstOrDefault(module => string.Equals(module.Id, moduleId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"找不到模組：{moduleId}");

        if (!moduleMetadata.IsLoadable)
        {
            throw new InvalidOperationException($"模組無法載入：{moduleMetadata.DisplayName}");
        }

        var stagedDllPath = ModulePackageCache.QueryStagedDllPath(
            moduleMetadata.FolderPath,
            moduleMetadata.DllPath,
            moduleMetadata.Id,
            "runtime");
        var assembly = Assembly.LoadFrom(stagedDllPath);
        EnsureWpfAssemblyResolve(assembly);
        var moduleType = QueryModuleType(assembly)
            ?? throw new InvalidOperationException($"模組缺少可載入入口：{moduleMetadata.DisplayName}");

        if (Activator.CreateInstance(moduleType) is not IModule module)
        {
            throw new InvalidOperationException($"模組入口建立失敗：{moduleMetadata.DisplayName}");
        }

        var moduleStateDirectoryPath = QueryModuleStateDirectoryPath(activeWorkspaceName, moduleId);
        var settingsPath = Path.Combine(moduleStateDirectoryPath, "settings.ini");
        var authPath = Path.Combine(moduleStateDirectoryPath, "auth.json");
        var context = new ModuleShellContext(
            activeWorkspaceName,
            moduleMetadata.Id,
            new ModuleSettingsStore(settingsPath),
            new ModuleAuthStore(authPath, settingsPath),
            moduleMetadata.RequiresLicense,
            licenseVerificationService,
            notificationRuntime,
            eventLogRepository);

        var loaded = new LoadedModuleHandle(cacheKey, moduleId, module, context);
        _loadedModules[cacheKey] = loaded;
        eventLogRepository.Record(EventLogLevel.Info, moduleId, "模組已載入");
        return loaded;
    }

    private string ResolveActiveWorkspaceName()
    {
        var configuredWorkspaceName = iniRepository.QuerySnapshot<Helix_Studio.Model.MainModel>(MainSection).Workspace ?? string.Empty;
        return workspacesRepository.ResolveWorkspaceNameOrFallback(configuredWorkspaceName);
    }

    private string QueryModuleStateDirectoryPath(string workspaceName, string moduleId)
    {
        var workspaceDirectoryPath = workspacesRepository.QueryWorkspaceDirectoryPath(workspaceName);
        var moduleStateDirectoryPath = Path.Combine(workspaceDirectoryPath, ModuleStateDirectoryName, moduleId);
        Directory.CreateDirectory(moduleStateDirectoryPath);
        return moduleStateDirectoryPath;
    }

    private static readonly Dictionary<string, Assembly> _runtimeAssemblies = new(StringComparer.OrdinalIgnoreCase);

    private static void EnsureWpfAssemblyResolve(Assembly assembly)
    {
        var name = assembly.GetName().Name;
        if (string.IsNullOrEmpty(name) || _runtimeAssemblies.ContainsKey(name))
            return;

        _runtimeAssemblies[name] = assembly;

        // WPF resolves BAML assembly via Assembly.Load(name) which may pick up a stale
        // metadata-ALC version still alive in the AppDomain. This handler guarantees
        // WPF always uses the runtime assembly for pack:// resource lookup.
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            var requested = new AssemblyName(args.Name).Name;
            return !string.IsNullOrEmpty(requested) && _runtimeAssemblies.TryGetValue(requested, out var asm)
                ? asm
                : null;
        };
    }

    private static Type? QueryModuleType(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes()
                .FirstOrDefault(type => !type.IsAbstract && typeof(IModule).IsAssignableFrom(type));
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types
                .Where(static type => type is not null)
                .Cast<Type>()
                .FirstOrDefault(type => !type.IsAbstract && typeof(IModule).IsAssignableFrom(type));
        }
    }

    private sealed record LoadedModuleHandle(
        string CacheKey,
        string ModuleId,
        IModule Module,
        ModuleShellContext Context)
    {
        public FrameworkElement? MainView { get; set; }

        public FrameworkElement? SettingsView { get; set; }
    }

    private sealed class ModuleShellContext(
        string activeWorkspaceName,
        string moduleId,
        ModuleSettingsStore settingsStore,
        ModuleAuthStore authStore,
        bool requiresLicense,
        LicenseVerificationService licenseVerificationService,
        NotificationRuntime notificationRuntime,
        EventLogRepository eventLogRepository) : IShellContext
    {
        private static readonly IReadOnlyDictionary<LogLevel, EventLogLevel> LogLevelMap =
            new Dictionary<LogLevel, EventLogLevel>
            {
                [LogLevel.Trace]       = EventLogLevel.Debug,
                [LogLevel.Debug]       = EventLogLevel.Debug,
                [LogLevel.Information] = EventLogLevel.Info,
                [LogLevel.Warning]     = EventLogLevel.Warning,
                [LogLevel.Error]       = EventLogLevel.Error,
                [LogLevel.Critical]    = EventLogLevel.Error,
            };

        public string ActiveWorkspaceName => activeWorkspaceName;

        public IModuleSettings Settings { get; } = settingsStore;

        public IModuleAuth Auth { get; } = authStore;

        public Action<string, LogLevel>? Log => (message, level) =>
        {
            var eventLevel = LogLevelMap.TryGetValue(level, out var mapped) ? mapped : EventLogLevel.Info;
            eventLogRepository.Record(eventLevel, moduleId, message);
        };

        public ModuleLicenseVerificationResult VerifyModuleLicense(string? licenseKey)
        {
            if (!requiresLicense)
            {
                return new ModuleLicenseVerificationResult
                {
                    IsValid = true,
                };
            }

            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                return new ModuleLicenseVerificationResult
                {
                    IsValid = false,
                    ErrorMessage = "此模組需要有效的模組金鑰。",
                };
            }

            return licenseVerificationService.VerifyLicense(moduleId, licenseKey);
        }

        public void ReportHealth(ModuleHealth health, string? detailText = null)
        {
            if (health != ModuleHealth.Faulted)
            {
                eventLogRepository.Record(EventLogLevel.Info, moduleId, detailText ?? $"模組狀態：{health}");
                return;
            }

            var message = detailText ?? "模組授權金鑰無效。";
            notificationRuntime.Request(new NotificationRequest
            {
                Title = "模組授權失效",
                Message = message,
                Severity = NotificationSeverity.Warning,
                DisplayMilliseconds = 6000,
            });
            eventLogRepository.Record(EventLogLevel.Warning, moduleId, message);
        }
    }

    private sealed class ModuleSettingsStore(string filePath) : IModuleSettings
    {
        private static readonly UTF8Encoding Utf8BomEncoding = new(true);
        private readonly object _sync = new();

        public string QueryString(string key, string fallback = "")
        {
            var values = QueryAllValues();
            return values.TryGetValue(key, out var value) ? value : fallback;
        }

        public bool QueryBoolean(string key, bool fallback)
        {
            var rawValue = QueryString(key, string.Empty);
            return bool.TryParse(rawValue, out var value) ? value : fallback;
        }

        public int QueryInt32(string key, int fallback)
        {
            var rawValue = QueryString(key, string.Empty);
            return int.TryParse(rawValue, out var value) ? value : fallback;
        }

        public ModuleSettingSaveResult SaveString(string key, string? value)
            => SaveValue(key, value ?? string.Empty);

        public ModuleSettingSaveResult SaveBoolean(string key, bool value)
            => SaveValue(key, value ? bool.TrueString : bool.FalseString);

        private ModuleSettingSaveResult SaveValue(string key, string value)
        {
            try
            {
                lock (_sync)
                {
                    var values = QueryAllValues();
                    values[key] = value;
                    Persist(values);
                }

                return new ModuleSettingSaveResult
                {
                    IsSuccess = true,
                };
            }
            catch (Exception ex)
            {
                return new ModuleSettingSaveResult
                {
                    IsSuccess = false,
                    DetailText = ex.Message,
                };
            }
        }

        private Dictionary<string, string> QueryAllValues()
        {
            if (!File.Exists(filePath))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('[') || line.StartsWith(';') || line.StartsWith('#'))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf('=', StringComparison.Ordinal);
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line[..separatorIndex].Trim();
                var value = separatorIndex == line.Length - 1 ? string.Empty : line[(separatorIndex + 1)..].Trim();
                if (key.Length == 0)
                {
                    continue;
                }

                result[key] = value;
            }

            return result;
        }

        private void Persist(IReadOnlyDictionary<string, string> values)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            var builder = new StringBuilder();
            builder.AppendLine("[Settings]");
            foreach (var entry in values.OrderBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                builder.Append(entry.Key)
                    .Append('=')
                    .AppendLine(entry.Value ?? string.Empty);
            }

            File.WriteAllText(filePath, builder.ToString(), Utf8BomEncoding);
        }
    }

    private sealed class ModuleAuthStore(string authFilePath, string legacySettingsFilePath) : IModuleAuth
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
        };

        private static readonly UTF8Encoding Utf8BomEncoding = new(true);
        private readonly object _sync = new();

        public string QueryLicenseKey()
        {
            lock (_sync)
            {
                var auth = QueryAuthDocument();
                if (!string.IsNullOrWhiteSpace(auth.LicenseKey))
                {
                    return auth.LicenseKey;
                }

                var legacyLicenseKey = QueryLegacyLicenseKey();
                if (string.IsNullOrWhiteSpace(legacyLicenseKey))
                {
                    return string.Empty;
                }

                Persist(new ModuleAuthDocument
                {
                    LicenseKey = legacyLicenseKey,
                });
                return legacyLicenseKey;
            }
        }

        public ModuleSettingSaveResult SaveLicenseKey(string? licenseKey)
        {
            try
            {
                lock (_sync)
                {
                    Persist(new ModuleAuthDocument
                    {
                        LicenseKey = (licenseKey ?? string.Empty).Trim(),
                    });
                }

                return new ModuleSettingSaveResult
                {
                    IsSuccess = true,
                };
            }
            catch (Exception ex)
            {
                return new ModuleSettingSaveResult
                {
                    IsSuccess = false,
                    DetailText = ex.Message,
                };
            }
        }

        private ModuleAuthDocument QueryAuthDocument()
        {
            if (!File.Exists(authFilePath))
            {
                return new ModuleAuthDocument();
            }

            try
            {
                var json = File.ReadAllText(authFilePath, Encoding.UTF8);
                return JsonSerializer.Deserialize<ModuleAuthDocument>(json) ?? new ModuleAuthDocument();
            }
            catch
            {
                return new ModuleAuthDocument();
            }
        }

        private string QueryLegacyLicenseKey()
        {
            if (!File.Exists(legacySettingsFilePath))
            {
                return string.Empty;
            }

            foreach (var rawLine in File.ReadAllLines(legacySettingsFilePath, Encoding.UTF8))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('[') || line.StartsWith(';') || line.StartsWith('#'))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf('=', StringComparison.Ordinal);
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line[..separatorIndex].Trim();
                if (!string.Equals(key, LegacyLicenseKeySettingsKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return separatorIndex == line.Length - 1
                    ? string.Empty
                    : line[(separatorIndex + 1)..].Trim();
            }

            return string.Empty;
        }

        private void Persist(ModuleAuthDocument document)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(authFilePath)!);
            var json = JsonSerializer.Serialize(document, JsonOptions);
            File.WriteAllText(authFilePath, json, Utf8BomEncoding);
        }

        private sealed class ModuleAuthDocument
        {
            public string LicenseKey { get; init; } = string.Empty;
        }
    }
}
