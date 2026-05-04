using System.IO;
using ExternalModules.InSpecTool.Core;

namespace ExternalModules.InSpecTool.Utilities;

public static class InSpecTargetPathUtility
{
    public static string? ResolveCurrentTargetPathForManualOutput(
        IEnumerable<PreviewFieldItem> fields,
        InSpecToolConfiguration configuration)
    {
        var orderedFields = fields.ToArray();
        var partNumber = QuerySpecialFieldValue(orderedFields, static field =>
            InSpecBarcodeUtility.QueryIsPartNumberField(field.Label, field.Keyword));
        var lotNumber = QuerySpecialFieldValue(orderedFields, static field =>
            InSpecBarcodeUtility.QueryIsLotField(field.Label, field.Keyword));
        var press = QuerySpecialFieldValue(orderedFields, static field =>
            InSpecBarcodeUtility.QueryIsPressField(field.Label, field.Keyword));

        if (string.IsNullOrWhiteSpace(partNumber) || string.IsNullOrWhiteSpace(lotNumber) || string.IsNullOrWhiteSpace(press))
        {
            return null;
        }

        var cleanPartNumber = SanitizeFileNamePart(partNumber);
        var cleanLotNumber = SanitizeFileNamePart(lotNumber);
        if (string.IsNullOrWhiteSpace(cleanPartNumber) || string.IsNullOrWhiteSpace(cleanLotNumber))
        {
            return null;
        }

        var pressEnglish = press.Trim();
        var dashIndex = pressEnglish.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex > 0)
        {
            pressEnglish = pressEnglish[..dashIndex];
        }

        var safePressDirectory = SanitizeFileNamePart(pressEnglish);
        if (string.IsNullOrWhiteSpace(safePressDirectory))
        {
            return null;
        }

        var groupFolder = SanitizeFileNamePart(DerivePartNumberGroupFolder(cleanPartNumber));
        if (string.IsNullOrWhiteSpace(groupFolder))
        {
            return null;
        }

        var rootDirectory = !string.IsNullOrWhiteSpace(configuration.SystemLogDirectory)
            ? configuration.SystemLogDirectory.Trim()
            : configuration.BackupSavePath.Trim();

        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            return null;
        }

        var partPrefix = cleanPartNumber[..1];
        var fileName = $"{cleanPartNumber}_{cleanLotNumber}.txt";
        var fullPath = Path.Combine(rootDirectory, safePressDirectory, partPrefix, groupFolder, fileName);
        return Path.GetFullPath(fullPath);
    }

    public static bool QueryCanOpenFile(IEnumerable<PreviewFieldItem> fields, out string message)
    {
        var missingFields = fields
            .Where(field => !field.QueryRequiresConfirmation() && string.IsNullOrWhiteSpace(field.Value))
            .Select(field => field.Label)
            .ToArray();

        if (missingFields.Length == 0)
        {
            message = string.Empty;
            return true;
        }

        message = $"請先完成以下欄位後再開啟檔案：{string.Join("、", missingFields)}";
        return false;
    }

    private static string QuerySpecialFieldValue(IEnumerable<PreviewFieldItem> fields, Func<PreviewFieldItem, bool> predicate)
    {
        foreach (var field in fields)
        {
            if (predicate(field))
            {
                return field.Value?.Trim() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static string DerivePartNumberGroupFolder(string partNumber)
    {
        if (string.IsNullOrWhiteSpace(partNumber))
        {
            return string.Empty;
        }

        var dashIndex = partNumber.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex <= 0 || dashIndex >= partNumber.Length - 1)
        {
            return partNumber.Trim();
        }

        var prefix = partNumber[..dashIndex].Trim();
        var suffix = partNumber[(dashIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(prefix) || string.IsNullOrWhiteSpace(suffix))
        {
            return partNumber.Trim();
        }

        return $"{prefix}-{suffix[0]}";
    }

    private static string SanitizeFileNamePart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var sanitized = value.Trim();
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalidCharacter, '_');
        }

        return sanitized.TrimEnd('.', ' ').Trim();
    }
}
