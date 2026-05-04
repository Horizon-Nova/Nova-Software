namespace ExternalModules.InSpecTool.Core;

public sealed class InSpecBarcodeParseResult
{
    public string RawBarcode { get; init; } = string.Empty;

    public string CleanedBarcode { get; init; } = string.Empty;

    public IReadOnlyList<string> Parts { get; init; } = [];

    public IReadOnlyList<string> FieldValues { get; init; } = [];

    public bool HasInput => !string.IsNullOrWhiteSpace(RawBarcode);

    public bool HasBlockedCharacter { get; init; }

    public bool HasLeadingSeparator { get; init; }

    public bool CanApplyToFields => !HasBlockedCharacter && FieldValues.Count > 0;

    public string QueryValidationMessage()
    {
        if (!HasInput)
        {
            return string.Empty;
        }

        if (HasBlockedCharacter)
        {
            return "條碼格式不正確，包含不允許的字元。";
        }

        return string.Empty;
    }
}
