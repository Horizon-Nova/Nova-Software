using System.IO;
using ExternalModules.InSpecTool.Core;

namespace ExternalModules.InSpecTool.Utilities;

public sealed class InSpecResultQueryUtility
{
    private readonly InSpecFileQueryUtility _fileQueryUtility = new();

    public (IReadOnlyList<PreviewResultItem> Results, string PlaceholderText) QueryResults(
        IEnumerable<PreviewFieldItem> fields,
        InSpecToolConfiguration configuration)
    {
        var orderedFields = fields.ToArray();
        var partNumber = QueryPartNumber(orderedFields);
        if (string.IsNullOrWhiteSpace(partNumber))
        {
            return ([], "請先輸入或掃描條碼。");
        }

        if (string.IsNullOrWhiteSpace(configuration.SearchDirectory))
        {
            return ([], "請先在模組設定中指定搜尋資料夾。");
        }

        var normalizedPartNumber = InSpecBarcodeUtility.NormalizePartNumberForQuery(partNumber);

        try
        {
            var results = _fileQueryUtility.QueryFiles(
                configuration.SearchDirectory,
                partNumber,
                normalizedPartNumber,
                configuration.SetupKeyword);

            return results.Count == 0
                ? ([], "找不到符合條件的 InSpec 檔案。")
                : (results, string.Empty);
        }
        catch (DirectoryNotFoundException exception)
        {
            return ([], exception.Message);
        }
        catch (Exception exception)
        {
            return ([], exception.Message);
        }
    }

    public void ClearFileIndex() => _fileQueryUtility.ClearIndex();

    private static string QueryPartNumber(IReadOnlyList<PreviewFieldItem> fields)
    {
        var partNumberField = fields.FirstOrDefault(field =>
            InSpecBarcodeUtility.QueryIsPartNumberField(field.Label, field.Keyword));
        if (partNumberField is not null)
        {
            return partNumberField.Value.Trim();
        }

        return fields.FirstOrDefault()?.Value.Trim() ?? string.Empty;
    }
}
