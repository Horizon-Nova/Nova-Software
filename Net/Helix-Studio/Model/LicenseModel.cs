namespace Helix_Studio.Model;

/// <summary>授權輸入模型。</summary>
public sealed record LicenseModel
{
    /// <summary>授權金鑰。</summary>
    public string LicenseKey { get; set; } = string.Empty;
}
