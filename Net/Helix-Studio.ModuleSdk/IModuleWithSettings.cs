using System.Windows;

namespace HorizonNova.HelixStudio.ModuleSdk;

/// <summary>
/// Optional extension of <see cref="IModule"/> for modules that provide their
/// own settings panel. The shell shows this panel in the Modules management page
/// when the user clicks on the module's settings tab.
/// </summary>
public interface IModuleWithSettings : IModule
{
    /// <summary>
    /// Create (or return a cached) WPF element shown as the module's settings
    /// panel inside the shell's Modules management page.
    /// </summary>
    FrameworkElement CreateSettingsView(IShellContext context);
}
