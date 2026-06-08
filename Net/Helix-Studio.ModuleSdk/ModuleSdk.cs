using System.Windows;
using Microsoft.Extensions.Logging;

namespace HorizonNova.HelixStudio.ModuleSdk;

public interface IModule : IDisposable
{
    FrameworkElement CreateView(IShellContext context);

    void OnActivated(IShellContext context);

    void OnDeactivated();
}

public interface IModuleWithSettings : IModule
{
    FrameworkElement CreateSettingsView(IShellContext context);
}

public interface IShellContext
{
    string ActiveWorkspaceName { get; }

    IModuleSettings Settings { get; }

    IModuleAuth Auth { get; }

    Action<string, LogLevel>? Log { get; }

    ModuleLicenseVerificationResult VerifyModuleLicense(string? licenseKey);

    void ReportHealth(ModuleHealth health, string? detailText = null);
}

public interface IModuleSettings
{
    string QueryString(string key, string fallback = "");

    bool QueryBoolean(string key, bool fallback);

    int QueryInt32(string key, int fallback);

    ModuleSettingSaveResult SaveString(string key, string? value);

    ModuleSettingSaveResult SaveBoolean(string key, bool value);
}

public interface IModuleAuth
{
    string QueryLicenseKey();

    ModuleSettingSaveResult SaveLicenseKey(string? licenseKey);
}

public enum ModuleHealth
{
    Healthy,
    Faulted
}

public sealed class ModuleLicenseVerificationResult
{
    public bool IsValid { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed class ModuleSettingSaveResult
{
    public bool IsSuccess { get; init; }

    public string DetailText { get; init; } = string.Empty;
}

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ModuleIdAttribute(string value) : Attribute
{
    public string Value { get; } = value;
}

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ModuleDisplayNameAttribute(string value) : Attribute
{
    public string Value { get; } = value;
}

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ModuleVersionAttribute(string value) : Attribute
{
    public string Value { get; } = value;
}

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ModuleAuthorAttribute(string value) : Attribute
{
    public string Value { get; } = value;
}

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ModuleDescriptionAttribute(string value) : Attribute
{
    public string Value { get; } = value;
}

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ModuleMinHostVersionAttribute(string value) : Attribute
{
    public string Value { get; } = value;
}

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ModuleIconResourceAttribute(string resourceName) : Attribute
{
    public string ResourceName { get; } = resourceName;
}

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ModuleRequiresLicenseAttribute : Attribute;
