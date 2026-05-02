namespace HorizonNova.HelixStudio.ModuleSdk;

/// <summary>
/// Health state reported by a module to the shell via
/// <see cref="IShellContext.ReportHealth"/>.
/// The shell status-bar indicator reflects this value in real time.
/// </summary>
public enum ModuleHealth
{
    /// <summary>Module is operating normally.</summary>
    Healthy = 0,

    /// <summary>
    /// Module has a non-fatal issue — e.g. license nearing expiry,
    /// optional dependency missing, or degraded functionality.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Module cannot operate correctly — e.g. license invalid,
    /// required resource unavailable, or a fatal initialisation error.
    /// </summary>
    Faulted = 2,
}
