using System.Windows;

namespace HorizonNova.HelixStudio.ModuleSdk;

/// <summary>
/// Contract every Helix Studio module must implement.
/// Decorate the assembly with <see cref="ModuleIdAttribute"/> and friends.
/// </summary>
public interface IModule : IDisposable
{
    /// <summary>
    /// Create (or return a cached) WPF element to display in the shell's
    /// right-hand content area.
    /// Called once on first activation; the shell keeps the returned element alive.
    /// </summary>
    FrameworkElement CreateView(IShellContext context);

    /// <summary>Called each time the module's tab/icon is selected.</summary>
    void OnActivated(IShellContext context);

    /// <summary>Called when the user navigates away from the module view.</summary>
    void OnDeactivated();
}

