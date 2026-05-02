namespace Helix_Studio.Model.License;

/// <summary>產品授權啟用後之持久化狀態欄位。</summary>
public sealed class LicenseActivationState
{
    /// <summary>遮罩後之授權金鑰（供顯示）。</summary>
    public string LicenseKeyMasked { get; set; } = string.Empty;

    /// <summary>綁定裝置名稱。</summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>裝置指紋 SHA-256 字串。</summary>
    public string DeviceFingerprintSha256 { get; set; } = string.Empty;

    /// <summary>啟用時間（UTC）。</summary>
    public DateTime ActivatedAtUtc { get; set; }

    /// <summary>授權到期時間（UTC）。</summary>
    public DateTime ExpiresAtUtc { get; set; }
}
