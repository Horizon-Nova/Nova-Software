using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Helix_Studio.Infrastructure.Paths;
using Helix_Studio.Model;

namespace Helix_Studio.Infrastructure.Licensing;

/// <summary>
/// 一般金鑰服務契約。
/// </summary>
public interface IApplicationLicenseService
{
    /// <summary>
    /// 讀取目前已儲存的一般金鑰。
    /// </summary>
    string QueryStoredLicenseKey();

    /// <summary>
    /// 儲存一般金鑰到 App Home Root 的授權檔。
    /// </summary>
    void SaveLicenseKey(string? licenseKey);

    /// <summary>
    /// 驗證指定的一般金鑰。
    /// </summary>
    ApplicationLicenseVerificationResult VerifyLicense(string? licenseKey);

    /// <summary>
    /// 驗證目前已儲存的一般金鑰。
    /// </summary>
    ApplicationLicenseVerificationResult VerifyStoredLicense();

    /// <summary>
    /// 查詢啟用畫面需要呈現的本機資訊。
    /// </summary>
    LicenseFingerprintSnapshot QueryFingerprintSnapshot();
}

/// <summary>
/// 管理主程式一般金鑰的讀寫、驗證與狀態查詢。
/// </summary>
internal sealed class ApplicationLicenseService(LicenseVerificationService verificationService) : IApplicationLicenseService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly UTF8Encoding Utf8BomEncoding = new(true);
    private readonly object _sync = new();

    /// <summary>
    /// 讀取目前已儲存的一般金鑰。
    /// </summary>
    public string QueryStoredLicenseKey()
    {
        lock (_sync)
        {
            return QueryLicenseDocument().LicenseKey ?? string.Empty;
        }
    }

    /// <summary>
    /// 儲存一般金鑰到 App Home Root 的授權檔。
    /// </summary>
    public void SaveLicenseKey(string? licenseKey)
    {
        lock (_sync)
        {
            Persist(new LicenseModel
            {
                LicenseKey = (licenseKey ?? string.Empty).Trim(),
            });
        }
    }

    /// <summary>
    /// 驗證指定的一般金鑰。
    /// </summary>
    public ApplicationLicenseVerificationResult VerifyLicense(string? licenseKey)
    {
        var verification = verificationService.VerifyLicense(string.Empty, licenseKey);
        return new ApplicationLicenseVerificationResult(
            verification.IsValid,
            verification.ErrorMessage ?? string.Empty);
    }

    /// <summary>
    /// 驗證目前已儲存的一般金鑰。
    /// </summary>
    public ApplicationLicenseVerificationResult VerifyStoredLicense()
        => VerifyLicense(QueryStoredLicenseKey());

    /// <summary>
    /// 查詢啟用畫面需要呈現的本機資訊。
    /// </summary>
    public LicenseFingerprintSnapshot QueryFingerprintSnapshot()
        => verificationService.QueryFingerprintSnapshot();

    private static string QueryAuthFilePath()
        => Path.Combine(AppPathProvider.QueryAppDataDirectory(), "auth.json");

    private LicenseModel QueryLicenseDocument()
    {
        var filePath = QueryAuthFilePath();
        if (!File.Exists(filePath))
        {
            return new LicenseModel();
        }

        try
        {
            var json = File.ReadAllText(filePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<LicenseModel>(json) ?? new LicenseModel();
        }
        catch
        {
            return new LicenseModel();
        }
    }

    private void Persist(LicenseModel document)
    {
        var filePath = QueryAuthFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(filePath, json, Utf8BomEncoding);
    }
}

/// <summary>
/// 表示主程式一般金鑰的驗證結果。
/// </summary>
/// <param name="IsValid">是否驗證通過。</param>
/// <param name="ErrorMessage">失敗時的錯誤訊息。</param>
public sealed record ApplicationLicenseVerificationResult(
    bool IsValid,
    string ErrorMessage);
