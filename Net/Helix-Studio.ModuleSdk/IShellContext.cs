using Microsoft.Extensions.Logging;

namespace HorizonNova.HelixStudio.ModuleSdk;

/// <summary>
/// Shell services exposed to a loaded module.
/// Obtain via <see cref="IModule.OnActivated"/>.
/// </summary>
public interface IShellContext
{
    /// <summary>Resolve a shell-registered service (e.g. ILogger&lt;T&gt;).</summary>
    T? GetService<T>() where T : class;

    /// <summary>Write a message to the shell event log.</summary>
    void Log(string message, LogLevel level = LogLevel.Information);

    /// <summary>Current active workspace name (empty string if none).</summary>
    string ActiveWorkspaceName { get; }

    /// <summary>
    /// Per-module settings store. Values survive across module deactivations
    /// and application restarts. Never null — returns an empty settings object
    /// when no settings have been saved yet.
    /// </summary>
    IModuleSettings Settings { get; }

    /// <summary>
    /// Report the module's current health to the shell.
    /// The shell status-bar indicator reflects this value immediately.
    /// Call from <see cref="IModule.OnActivated"/> and whenever health changes
    /// (e.g. after a license check, a connection attempt, etc.).
    /// </summary>
    /// <param name="health">The health level to display.</param>
    /// <param name="message">Optional tooltip text shown next to the status light.</param>
    void ReportHealth(ModuleHealth health, string? message = null);

    /// <summary>
    /// Verify a module license key against the current machine's hardware
    /// fingerprints. Call from <see cref="IModule.OnActivated"/> and use the
    /// result to decide whether to restrict functionality and what health to
    /// report via <see cref="ReportHealth"/>.
    /// </summary>
    /// <param name="licenseKey">Key in <c>GUID:SHA256HASH</c> format.</param>
    ModuleLicenseResult VerifyModuleLicense(string licenseKey);
}

