namespace HorizonNova.HelixStudio.ModuleSdk;

/// <summary>
/// 模組身分資訊快照。
/// 由主程式掃描組件屬性後產生；模組端不應直接寫入或自行建構此物件。
/// </summary>
public sealed record ModuleManifest
{
    /// <summary>反向網域的唯一識別，例如「com.horizonnova.demo」。</summary>
    public required string Id { get; init; }

    /// <summary>顯示名稱，用於側邊欄與模組管理畫面。</summary>
    public required string DisplayName { get; init; }

    /// <summary>語意化版本字串，例如「1.2.3」。</summary>
    public required string Version { get; init; }

    /// <summary>作者/組織名稱。</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>簡短描述，用於細節面板顯示。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>此模組所需的最低 Helix Studio 主程式版本。</summary>
    public string MinHostVersion { get; init; } = "1.0.0";

    /// <summary>模組主 DLL 的絕對路徑。</summary>
    public required string DllPath { get; init; }

    /// <summary>
    /// 從模組嵌入資源載入的 PNG 圖示位元組。
    /// 若未宣告 <see cref="ModuleIconResourceAttribute"/> 或載入失敗則為 null。
    /// </summary>
    public byte[]? IconPngBytes { get; init; }

    /// <summary>
    /// 模組安裝資料夾的絕對路徑。
    /// 由主程式載入器設定；模組端不應寫入。
    /// </summary>
    public string InstalledFolderPath { get; init; } = string.Empty;

    /// <summary>
    /// 當模組組件宣告 <see cref="ModuleRequiresLicenseAttribute"/> 時為 true。
    /// 主程式將在啟用模組前執行硬體綁定授權金鑰檢查。
    /// </summary>
    public bool RequiresLicense { get; init; }
}
