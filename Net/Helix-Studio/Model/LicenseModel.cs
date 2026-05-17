namespace Helix_Studio.Model;

/// <summary>
/// 授權金鑰模型。
/// </summary>
public sealed record LicenseModel
{
    /// <summary>
    /// 授權金鑰字串。
    /// </summary>
    public string LicenseKey { get; set; } = string.Empty;
}

/// <summary>
/// 模組授權驗證結果模型。
/// </summary>
public sealed record ModuleLicenseResultModel
{
    /// <summary>
    /// 是否通過驗證。
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// 失敗原因（通過時為空字串）。
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 建立通過結果。
    /// </summary>
    public static ModuleLicenseResultModel Valid()
        => new() { IsValid = true, Message = string.Empty };

    /// <summary>
    /// 建立失敗結果。
    /// </summary>
    /// <param name="message">錯誤訊息。</param>
    public static ModuleLicenseResultModel Invalid(string message)
        => new() { IsValid = false, Message = message };
}

