using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Helix_Studio.Model.License;

namespace Helix_Studio.Domain.License;

/// <summary>授權金鑰格式、裝置指紋與啟用狀態之純領域規則。</summary>
public static class LicenseDomain
{
    private static readonly Regex LicenseFormatRegex =
        new("^HN-[A-Z0-9]{4}(?:-[A-Z0-9]{4}){5}$", RegexOptions.Compiled);

    /// <summary>將輸入金鑰正規化為大寫並去除首尾空白。</summary>
    public static string NormalizeLicenseKey(string input)
        => (input ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>驗證正規化後金鑰格式；不符時擲出例外。</summary>
    public static void EnsureLicenseKeyValid(string normalizedLicenseKey, string argName)
    {
        if (string.IsNullOrEmpty(normalizedLicenseKey))
        {
            throw new ArgumentException("授權金鑰不可為空白。", argName);
        }

        if (!LicenseFormatRegex.IsMatch(normalizedLicenseKey))
        {
            throw new FormatException("授權金鑰格式錯誤，請使用 HN-XXXX-XXXX-XXXX-XXXX-XXXX-XXXX。");
        }
    }

    /// <summary>將金鑰中間區段遮罩以供顯示。</summary>
    public static string MaskLicenseKey(string normalized)
    {
        if (normalized.Length < 8)
        {
            return normalized;
        }

        return $"{normalized[..7]}-****-****-****-****-{normalized[^4..]}";
    }

    /// <summary>依本機環境資訊計算 SHA-256 裝置指紋字串。</summary>
    public static string QueryDeviceFingerprintSha256()
    {
        var raw = string.Join("|",
            Environment.MachineName,
            Environment.UserName,
            Environment.UserDomainName,
            Environment.OSVersion.VersionString);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    /// <summary>依已驗證之金鑰建立啟用狀態快照（含到期時間）。</summary>
    public static LicenseActivationState BuildActivationState(string normalizedLicenseKey)
    {
        var activatedAt = DateTime.UtcNow;
        return new LicenseActivationState
        {
            LicenseKeyMasked = MaskLicenseKey(normalizedLicenseKey),
            DeviceName = Environment.MachineName,
            DeviceFingerprintSha256 = QueryDeviceFingerprintSha256(),
            ActivatedAtUtc = activatedAt,
            ExpiresAtUtc = activatedAt.AddYears(1),
        };
    }
}
