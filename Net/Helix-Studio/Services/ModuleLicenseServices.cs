using HorizonNova.HelixStudio.ModuleSdk;
using Helix_Studio.Infrastructure.License;

namespace Helix_Studio.Services;

/// <summary>
/// 模組授權驗證服務，封裝硬體綁定金鑰的驗證流程。
/// </summary>
public sealed class ModuleLicenseServices
{
    /// <summary>
    /// 驗證模組授權金鑰是否可在此裝置使用。
    /// </summary>
    /// <param name="moduleId">模組唯一識別。</param>
    /// <param name="licenseKey">授權金鑰（GUID:SHA256）。</param>
    /// <returns>驗證結果。</returns>
    public ModuleLicenseResult Verify(string moduleId, string licenseKey)
        => ModuleLicenseVerifier.Verify(moduleId, licenseKey);
}

