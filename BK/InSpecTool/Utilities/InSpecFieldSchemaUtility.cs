using System.Text.Json;
using ExternalModules.InSpecTool.Core;

namespace ExternalModules.InSpecTool.Utilities;

public static class InSpecFieldSchemaUtility
{
    public static IReadOnlyList<InSpecFieldDefinition> QueryDefinitions(string rawSchema)
    {
        var source = string.IsNullOrWhiteSpace(rawSchema) ? string.Empty : rawSchema.Trim();
        var definitions = source.StartsWith("[", StringComparison.Ordinal)
            ? QueryDefinitionsFromJson(source)
            : QueryDefinitionsFromLegacySchema(source);

        return definitions.Count == 0
            ? QueryDefaultDefinitions()
            : definitions;
    }

    private static IReadOnlyList<InSpecFieldDefinition> QueryDefinitionsFromJson(string rawSchema)
    {
        try
        {
            using var document = JsonDocument.Parse(rawSchema);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var definitions = new List<InSpecFieldDefinition>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = QueryString(item, "name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                definitions.Add(new InSpecFieldDefinition
                {
                    Name = name,
                    Keyword = QueryString(item, "keyword", name),
                    TargetMode = InSpecFieldDefinition.NormalizeTargetMode(
                        QueryString(item, "targetMode", InSpecFieldDefinition.DirectInputTargetMode)),
                    IsEditable = QueryBoolean(item, "isEditable", true)
                });
            }

            return definitions;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<InSpecFieldDefinition> QueryDefinitionsFromLegacySchema(string rawSchema)
    {
        if (string.IsNullOrWhiteSpace(rawSchema))
        {
            return [];
        }

        var definitions = new List<InSpecFieldDefinition>();
        foreach (var segment in rawSchema.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = segment.Split(',', 3, StringSplitOptions.TrimEntries);
            if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
            {
                continue;
            }

            var editable = true;
            if (parts.Length > 1 && bool.TryParse(parts[1], out var parsedEditable))
            {
                editable = parsedEditable;
            }

            var targetMode = InSpecFieldDefinition.NormalizeTargetMode(
                parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2])
                    ? parts[2]
                    : InSpecFieldDefinition.DirectInputTargetMode);

            definitions.Add(new InSpecFieldDefinition
            {
                Name = parts[0],
                Keyword = parts[0],
                IsEditable = editable,
                TargetMode = targetMode
            });
        }

        return definitions;
    }

    private static IReadOnlyList<InSpecFieldDefinition> QueryDefaultDefinitions() =>
    [
        new InSpecFieldDefinition
        {
            Name = "料號",
            Keyword = "part number",
            IsEditable = true,
            TargetMode = InSpecFieldDefinition.DirectInputTargetMode
        },
        new InSpecFieldDefinition
        {
            Name = "批號",
            Keyword = "lot number",
            IsEditable = true,
            TargetMode = InSpecFieldDefinition.DirectInputTargetMode
        },
        new InSpecFieldDefinition
        {
            Name = "壓合",
            Keyword = "press",
            IsEditable = false,
            TargetMode = InSpecFieldDefinition.PromptConfirmationTargetMode
        },
        new InSpecFieldDefinition
        {
            Name = "機台",
            Keyword = "machine",
            IsEditable = true,
            TargetMode = InSpecFieldDefinition.DirectInputTargetMode
        },
        new InSpecFieldDefinition
        {
            Name = "操作員",
            Keyword = "operator",
            IsEditable = true,
            TargetMode = InSpecFieldDefinition.DirectInputTargetMode
        }
    ];

    private static string QueryString(JsonElement element, string propertyName, string defaultValue = "")
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return defaultValue;
        }

        var value = property.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static bool QueryBoolean(JsonElement element, string propertyName, bool defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var parsedValue) => parsedValue,
            _ => defaultValue
        };
    }
}
