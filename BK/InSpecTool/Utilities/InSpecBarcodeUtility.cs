using ExternalModules.InSpecTool.Core;

namespace ExternalModules.InSpecTool.Utilities;

public static class InSpecBarcodeUtility
{
    private static readonly string[][] PartNumberKeywordSets =
    [
        ["part", "number"],
        ["料號"]
    ];

    private static readonly string[][] LotKeywordSets =
    [
        ["lot"],
        ["批號"]
    ];

    private static readonly string[][] PressKeywordSets =
    [
        ["press"],
        ["壓合"]
    ];

    public static InSpecBarcodeParseResult ParseBarcode(string barcode, IReadOnlyList<InSpecFieldDefinition> definitions)
    {
        var values = Enumerable.Repeat(string.Empty, definitions.Count).ToArray();
        var rawBarcode = barcode ?? string.Empty;
        if (definitions.Count == 0)
        {
            return new InSpecBarcodeParseResult
            {
                RawBarcode = rawBarcode,
                CleanedBarcode = string.Empty,
                FieldValues = values,
                Parts = []
            };
        }

        var cleanedBarcode = rawBarcode.Trim();
        if (string.IsNullOrWhiteSpace(cleanedBarcode))
        {
            return new InSpecBarcodeParseResult
            {
                RawBarcode = rawBarcode,
                CleanedBarcode = string.Empty,
                FieldValues = values,
                Parts = []
            };
        }

        var hasLeadingSeparator = false;
        if (cleanedBarcode.StartsWith('|'))
        {
            hasLeadingSeparator = true;
            cleanedBarcode = cleanedBarcode[1..];
        }

        if (cleanedBarcode.Contains('_'))
        {
            return new InSpecBarcodeParseResult
            {
                RawBarcode = rawBarcode,
                CleanedBarcode = cleanedBarcode,
                FieldValues = values,
                Parts = [],
                HasBlockedCharacter = true,
                HasLeadingSeparator = hasLeadingSeparator
            };
        }

        var parts = cleanedBarcode
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var index = 0; index < Math.Min(parts.Length, definitions.Count); index++)
        {
            values[index] = QueryIsPartNumberField(definitions[index].Name, definitions[index].Keyword)
                ? NormalizePartNumberForQuery(parts[index])
                : parts[index];
        }

        return new InSpecBarcodeParseResult
        {
            RawBarcode = rawBarcode,
            CleanedBarcode = cleanedBarcode,
            Parts = parts,
            FieldValues = values,
            HasLeadingSeparator = hasLeadingSeparator
        };
    }

    public static IReadOnlyList<string> QueryFieldValues(string barcode, IReadOnlyList<InSpecFieldDefinition> definitions)
    {
        var result = ParseBarcode(barcode, definitions);
        return result.FieldValues;
    }

    public static string ComposeBarcode(IEnumerable<PreviewFieldItem> fields, IReadOnlyList<InSpecFieldDefinition> definitions)
    {
        var fieldValues = fields.ToDictionary(field => field.Label, field => field.Value, StringComparer.OrdinalIgnoreCase);
        var parts = new List<string>();

        foreach (var definition in definitions)
        {
            if (!fieldValues.TryGetValue(definition.Name, out var value) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            parts.Add(value.Trim());
        }

        return parts.Count == 0 ? string.Empty : $"|{string.Join("|", parts)}|";
    }

    public static string NormalizePartNumberForQuery(string partNumber)
    {
        if (string.IsNullOrWhiteSpace(partNumber))
        {
            return string.Empty;
        }

        var normalized = partNumber.Trim().ToUpperInvariant();
        var lastDashIndex = normalized.LastIndexOf('-');
        if (lastDashIndex < 0 || lastDashIndex >= normalized.Length - 1)
        {
            return normalized;
        }

        var lastPart = normalized[(lastDashIndex + 1)..];
        var letters = new List<char>();

        foreach (var character in lastPart)
        {
            if (!char.IsLetter(character))
            {
                break;
            }

            letters.Add(character);
        }

        return letters.Count == 0
            ? normalized
            : normalized[..(lastDashIndex + 1)] + new string([.. letters]);
    }

    public static bool QueryIsPartNumberField(params string?[] candidates) =>
        QueryMatchesAnyKeywordSet(candidates, PartNumberKeywordSets);

    public static bool QueryIsLotField(params string?[] candidates) =>
        QueryMatchesAnyKeywordSet(candidates, LotKeywordSets);

    public static bool QueryIsPressField(params string?[] candidates) =>
        QueryMatchesAnyKeywordSet(candidates, PressKeywordSets);

    private static bool QueryMatchesAnyKeywordSet(IEnumerable<string?> candidates, IEnumerable<string[]> keywordSets)
    {
        var source = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => candidate!.Trim())
            .ToArray();

        if (source.Length == 0)
        {
            return false;
        }

        foreach (var keywordSet in keywordSets)
        {
            foreach (var candidate in source)
            {
                if (keywordSet.All(keyword => candidate.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
