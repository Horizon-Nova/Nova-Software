using HN_Studio.ModuleSdk;

namespace ExternalModules.DeploymentTool.Utilities;

public static class DeploymentToolSettingsViewUtility
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

    public static string QuerySavedValue(IHnModuleSettings settings, string key, string fallback = "") =>
        settings.QueryString(key, fallback);

    public static bool QuerySavedBoolean(IHnModuleSettings settings, string key, bool fallback) =>
        settings.QueryBoolean(key, fallback);

    public static HnModuleSettingSaveResult SaveString(IHnModuleSettings settings, string key, string? rawValue) =>
        settings.SaveValue(key, rawValue ?? string.Empty);

    public static HnModuleSettingSaveResult SaveBoolean(IHnModuleSettings settings, string key, bool value) =>
        settings.SaveValue(key, value ? "true" : "false");
}
