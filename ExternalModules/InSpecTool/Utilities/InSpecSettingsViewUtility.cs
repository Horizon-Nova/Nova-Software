using System.Text.Json;
using ExternalModules.InSpecTool.Core;
using HorizonNova.HelixStudio.ModuleSdk;

namespace ExternalModules.InSpecTool.Utilities;

public static class InSpecSettingsViewUtility
{
    public static IReadOnlyList<string> QueryScanIntervalOptions() =>
    [
        "80",
        "120",
        "160",
        "200",
        "300",
        "500"
    ];

    public static IReadOnlyList<string> QueryMonitorIntervalOptions() =>
    [
        "200",
        "300",
        "500",
        "800",
        "1000"
    ];

    public static IReadOnlyList<string> QueryTargetModes() =>
    [
        InSpecFieldDefinition.DirectInputTargetMode,
        InSpecFieldDefinition.PromptConfirmationTargetMode
    ];

    public static string QuerySavedValue(IModuleSettings settings, string key, string fallback = "")
    {
        var value = settings.QueryString(key, fallback);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    public static bool QuerySavedBoolean(IModuleSettings settings, string key, bool fallback) =>
        settings.QueryBoolean(key, fallback);

    public static IReadOnlyList<InSpecSettingsFieldRow> QueryFieldRows(IModuleSettings settings)
    {
        var definitions = InSpecFieldSchemaUtility.QueryDefinitions(settings.QueryString("fieldSchema", string.Empty));
        return definitions
            .Select(definition => new InSpecSettingsFieldRow
            {
                Name = definition.Name,
                Keyword = definition.Keyword,
                TargetMode = InSpecFieldDefinition.NormalizeTargetMode(definition.TargetMode),
                IsEditable = definition.IsEditable
            })
            .ToArray();
    }

    public static string SerializeFieldRows(IEnumerable<InSpecSettingsFieldRow> rows)
    {
        var payload = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Name))
            .Select(row => new
            {
                name = row.Name.Trim(),
                keyword = string.IsNullOrWhiteSpace(row.Keyword) ? row.Name.Trim() : row.Keyword.Trim(),
                targetMode = InSpecFieldDefinition.NormalizeTargetMode(row.TargetMode),
                isEditable = row.IsEditable
            })
            .ToArray();

        return JsonSerializer.Serialize(payload);
    }

    public static ModuleSettingSaveResult SaveString(IModuleSettings settings, string key, string? value) =>
        settings.SaveString(key, value);

    public static ModuleSettingSaveResult SaveBoolean(IModuleSettings settings, string key, bool value) =>
        settings.SaveBoolean(key, value);
}
