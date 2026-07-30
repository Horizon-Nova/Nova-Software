using System.ComponentModel.DataAnnotations;

namespace Vaeron._App_.Feature;

/// <summary>公開表面：設定。啟動時驗證,不留到首次取用。</summary>
public sealed class FeatureOptions
{
    public const string SectionName = "Feature";

    [Required]
    [MinLength(1)]
    public string Source { get; init; } = string.Empty;

    [Range(1, 3600)]
    public int TimeoutSeconds { get; init; } = 30;
}
