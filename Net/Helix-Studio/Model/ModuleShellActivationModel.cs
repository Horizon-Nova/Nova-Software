using HorizonNova.HelixStudio.ModuleSdk;

namespace Helix_Studio.Model.Modules;

/// <summary>殼層啟用外掛模組之結果狀態（供 View／ViewModel 使用，不依賴 Infrastructure）。</summary>
public enum ShellModuleActivationStatus
{
    /// <summary>成功並已取得模組檢視。</summary>
    Success,

    /// <summary>需要授權金鑰。</summary>
    LicenseRequired,

    /// <summary>載入或建立執行個體失敗。</summary>
    LoadFailed,
}

/// <summary>模組啟用嘗試之結果（含檢視或授權驗證結果）。</summary>
public sealed record ShellModuleActivationOutcome
{
    /// <summary>結果狀態。</summary>
    public ShellModuleActivationStatus Status { get; init; }

    /// <summary>成功時之模組檢視物件。</summary>
    public object? View { get; init; }

    /// <summary>需要授權時之驗證結果。</summary>
    public ModuleLicenseResult? LicenseResult { get; init; }

    /// <summary>建立成功結果。</summary>
    public static ShellModuleActivationOutcome Ok(object view)
        => new() { Status = ShellModuleActivationStatus.Success, View = view };

    /// <summary>建立需要授權金鑰之結果。</summary>
    public static ShellModuleActivationOutcome KeyRequired(ModuleLicenseResult lr)
        => new() { Status = ShellModuleActivationStatus.LicenseRequired, LicenseResult = lr };

    /// <summary>建立載入失敗結果。</summary>
    public static ShellModuleActivationOutcome Failed()
        => new() { Status = ShellModuleActivationStatus.LoadFailed };
}

/// <summary>寫入模組授權金鑰時傳遞給命令的承載資料。</summary>
public sealed record SaveModuleLicensePayload(string ModuleId, string LicenseKey);
