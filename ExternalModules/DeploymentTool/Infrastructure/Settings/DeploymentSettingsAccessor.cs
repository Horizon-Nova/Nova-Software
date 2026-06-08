using HorizonNova.HelixStudio.ModuleSdk;

namespace ExternalModules.DeploymentTool.Infrastructure.Settings;

public static class DeploymentSettingsAccessor
{
    public static IReadOnlyList<string> QueryLinuxTemplateOptions() =>
    [
        "Ubuntu 24.04 + Docker Base",
        "Ubuntu 22.04 + Docker Base",
        "Debian 12 + Docker Base"
    ];

    public static IReadOnlyList<string> QueryDefaultCpuOptions() => ["2", "4", "6", "8", "12"];

    public static IReadOnlyList<string> QueryDefaultMemoryOptions() => ["4096", "8192", "12288", "16384", "24576"];

    public static IReadOnlyList<string> QueryDockerModeOptions() => ["docker compose", "Dockerfile only"];

    public static string QuerySavedValue(IModuleSettings settings, string key, string fallback = "") =>
        settings.QueryString(key, fallback);

    public static bool QuerySavedBoolean(IModuleSettings settings, string key, bool fallback) =>
        settings.QueryBoolean(key, fallback);

    public static ModuleSettingSaveResult SaveString(IModuleSettings settings, string key, string? rawValue) =>
        settings.SaveString(key, rawValue ?? string.Empty);

    public static ModuleSettingSaveResult SaveBoolean(IModuleSettings settings, string key, bool value) =>
        settings.SaveBoolean(key, value);
}
