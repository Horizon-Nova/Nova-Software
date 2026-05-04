using ExternalModules.InSpecTool.Core;
using ExternalModules.InSpecTool.Utilities;
using HN_Studio.ModuleSdk;

namespace ExternalModules.InSpecTool.Services;

public sealed class InSpecToolService
{
    private readonly InSpecResultQueryUtility _resultQueryUtility = new();

    public InSpecToolConfiguration QueryConfiguration(HnModuleContext? context) =>
        InSpecConfigurationUtility.QueryConfiguration(context);

    public IReadOnlyList<PreviewFieldItem> QueryFieldItems(string barcode, IReadOnlyList<InSpecFieldDefinition> definitions) =>
        InSpecFieldItemUtility.QueryFieldItems(QueryBarcodeParseResult(barcode, definitions), definitions);

    public InSpecBarcodeParseResult QueryBarcodeParseResult(string barcode, IReadOnlyList<InSpecFieldDefinition> definitions) =>
        InSpecBarcodeUtility.ParseBarcode(barcode, definitions);

    public string ComposeBarcode(IEnumerable<PreviewFieldItem> fields, IReadOnlyList<InSpecFieldDefinition> definitions) =>
        InSpecBarcodeUtility.ComposeBarcode(fields, definitions);

    public (IReadOnlyList<PreviewResultItem> Results, string PlaceholderText) QueryResults(
        IEnumerable<PreviewFieldItem> fields,
        InSpecToolConfiguration configuration) =>
        _resultQueryUtility.QueryResults(fields, configuration);

    public string? ResolveCurrentTargetPathForManualOutput(
        IEnumerable<PreviewFieldItem> fields,
        InSpecToolConfiguration configuration) =>
        InSpecTargetPathUtility.ResolveCurrentTargetPathForManualOutput(fields, configuration);

    public void ClearFileIndex() =>
        _resultQueryUtility.ClearFileIndex();

    public bool QueryCanOpenFile(IEnumerable<PreviewFieldItem> fields, out string message) =>
        InSpecTargetPathUtility.QueryCanOpenFile(fields, out message);

    public void OpenFile(PreviewResultItem resultItem, InSpecToolConfiguration configuration) =>
        InSpecFileLaunchUtility.OpenFile(resultItem, configuration.EnableAutoRun);

    public InSpecFieldDefinition? QueryDefinition(IEnumerable<InSpecFieldDefinition> definitions, string fieldName) =>
        definitions.FirstOrDefault(definition => string.Equals(definition.Name, fieldName, StringComparison.Ordinal));
}
