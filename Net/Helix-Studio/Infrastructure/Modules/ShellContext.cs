using Helix_Studio.Infrastructure.License;
using HorizonNova.HelixStudio.ModuleSdk;
using MsLogLevel   = Microsoft.Extensions.Logging.LogLevel;
using ShellLogLevel = Helix_Studio.Model.EventLog.LogLevel;

namespace Helix_Studio.Infrastructure.Modules;

/// <summary>提供模組使用之殼層上下文：日誌、設定、授權驗證與健康狀態回報。</summary>
internal sealed class ShellContext : IShellContext
{
    private readonly string              _moduleId;
    private readonly Func<Type, object?> _serviceResolver;
    private readonly Func<string>        _activeWorkspaceName;

    private ModuleHealth _currentHealth = ModuleHealth.Healthy;

    internal event Action<ModuleHealth, string?>? HealthChanged;

    /// <summary>建立指定模組之殼層上下文。</summary>
    public ShellContext(
        string moduleId,
        Func<Type, object?> serviceResolver,
        Func<string> activeWorkspaceName)
    {
        _moduleId            = moduleId;
        _serviceResolver     = serviceResolver;
        _activeWorkspaceName = activeWorkspaceName;
        Settings             = new ModuleSettingsStore(moduleId);
    }

    /// <inheritdoc />
    public T? GetService<T>() where T : class
        => _serviceResolver(typeof(T)) as T;

    /// <inheritdoc />
    public void Log(string message, MsLogLevel level = MsLogLevel.Information)
    {
        var shellLevel = level switch
        {
            MsLogLevel.Warning or MsLogLevel.Critical => ShellLogLevel.Warning,
            MsLogLevel.Error                          => ShellLogLevel.Error,
            MsLogLevel.Debug  or MsLogLevel.Trace     => ShellLogLevel.Debug,
            _                                         => ShellLogLevel.Info,
        };
        Services.EventLogServices.Log(shellLevel, message, "Module");
    }

    /// <inheritdoc />
    public void ReportHealth(ModuleHealth health, string? message = null)
    {
        _currentHealth = health;
        HealthChanged?.Invoke(health, message);
    }

    /// <inheritdoc />
    public ModuleLicenseResult VerifyModuleLicense(string licenseKey)
        => ModuleLicenseVerifier.Verify(_moduleId, licenseKey);

    /// <inheritdoc />
    public string          ActiveWorkspaceName => _activeWorkspaceName();

    /// <inheritdoc />
    public IModuleSettings Settings            { get; }

    internal ModuleHealth  CurrentHealth       => _currentHealth;
}
