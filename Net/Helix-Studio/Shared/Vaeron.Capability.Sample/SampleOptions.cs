using System.ComponentModel.DataAnnotations;

namespace Vaeron.Capability.Sample;

/// <summary>公開表面：設定。於啟動時驗證,不留到首次取用。</summary>
public sealed class SampleOptions
{
    public const string SectionName = "Sample";

    [Required]
    [MinLength(1)]
    public string Endpoint { get; init; } = string.Empty;

    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 30;
}
