using System.Windows;
using ExternalModules.InSpecTool.Views;
using HorizonNova.HelixStudio.ModuleSdk;

namespace ExternalModules.InSpecTool;

/// <summary>
/// Entry point for the InSpecTool module.
/// Implements both <see cref="IModule"/> (main view) and
/// <see cref="IModuleWithSettings"/> (settings panel).
/// </summary>
public sealed class InSpecTool : IModuleWithSettings
{
    /// <summary>
    /// Settings key under which the shell stores the module's license key.
    /// Must match <c>ModuleRuntimeServices.LicenseKeySettingsKey</c>.
    /// </summary>
    private const string LicenseKeySettingsKey = "_license_key";

    private InSpecToolView?         _view;
    private InSpecToolSettingsView? _settingsView;

    // ── IModule ────────────────────────────────────────────────────────────

    public FrameworkElement CreateView(IShellContext context)
    {
        _view = new InSpecToolView();
        _view.ApplyHostContext(context);
        return _view;
    }

    public void OnActivated(IShellContext context)
    {
        // Verify the stored license key and report health so the status-bar
        // light reflects the current license state on every navigation.
        var storedKey = context.Settings.QueryString(LicenseKeySettingsKey);
        var result    = context.VerifyModuleLicense(storedKey);

        if (result.IsValid)
            context.ReportHealth(ModuleHealth.Healthy);
        else
            context.ReportHealth(ModuleHealth.Faulted,
                string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "授權金鑰無效"
                    : result.ErrorMessage);

        _view?.ApplyHostContext(context);
    }

    public void OnDeactivated()
    {
        _view?.ClearHostContext();
    }

    // ── IModuleWithSettings ────────────────────────────────────────────────

    public FrameworkElement CreateSettingsView(IShellContext context)
    {
        if (_settingsView is null)
        {
            _settingsView = new InSpecToolSettingsView();
            _settingsView.ApplyHostContext(context);
        }
        return _settingsView;
    }

    // ── IDisposable ────────────────────────────────────────────────────────

    public void Dispose()
    {
        _view?.ClearHostContext();
        _settingsView?.ClearHostContext();
        _view         = null;
        _settingsView = null;
    }
}
