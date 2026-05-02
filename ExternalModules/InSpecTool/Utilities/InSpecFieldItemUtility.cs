using ExternalModules.InSpecTool.Core;

namespace ExternalModules.InSpecTool.Utilities;

public static class InSpecFieldItemUtility
{
    public static IReadOnlyList<PreviewFieldItem> QueryFieldItems(
        InSpecBarcodeParseResult parseResult,
        IReadOnlyList<InSpecFieldDefinition> definitions)
    {
        return definitions
            .Select((definition, index) => new PreviewFieldItem
            {
                Label = definition.Name,
                Value = index < parseResult.FieldValues.Count ? parseResult.FieldValues[index] : string.Empty,
                IsReadOnly = !definition.IsEditable,
                Keyword = definition.Keyword,
                TargetMode = definition.TargetMode
            })
            .ToArray();
    }
}
