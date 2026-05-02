namespace ExternalModules.InSpecTool.Core;

public sealed class InSpecFieldDefinition
{
    public const string DirectInputTargetMode = "直接輸入";
    public const string PromptConfirmationTargetMode = "提示確認";

    public string Name { get; init; } = string.Empty;

    public string Keyword { get; init; } = string.Empty;

    public bool IsEditable { get; init; } = true;

    public string TargetMode { get; init; } = DirectInputTargetMode;

    /// <summary>將舊版英文或空白正規化為目前使用的中文模式字串。</summary>
    public static string NormalizeTargetMode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DirectInputTargetMode;
        }

        var trimmed = raw.Trim();
        if (string.Equals(trimmed, "Direct Input", StringComparison.OrdinalIgnoreCase))
        {
            return DirectInputTargetMode;
        }

        if (string.Equals(trimmed, "Prompt Confirmation", StringComparison.OrdinalIgnoreCase))
        {
            return PromptConfirmationTargetMode;
        }

        return trimmed;
    }

    public bool QueryRequiresConfirmation() =>
        string.Equals(NormalizeTargetMode(TargetMode), PromptConfirmationTargetMode, StringComparison.Ordinal);
}
