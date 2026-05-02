namespace HorizonNova.HelixStudio.ModuleSdk;

/// <summary>模組寫入設定值時回傳的結果。</summary>
public sealed record ModuleSettingSaveResult
{
    /// <summary>表示儲存成功的共用實例。</summary>
    public static readonly ModuleSettingSaveResult Ok = new() { IsSuccess = true };

    /// <summary>是否儲存成功。</summary>
    public bool IsSuccess { get; init; }

    /// <summary>成功時已寫入值的字串表示；失敗時可能為空字串。</summary>
    public string SavedValue { get; init; } = string.Empty;

    /// <summary>失敗時的詳細說明；成功時通常為空字串。</summary>
    public string DetailText { get; init; } = string.Empty;

    /// <summary>建立表示儲存失敗的結果，並帶入 <paramref name="detail"/> 說明。</summary>
    public static ModuleSettingSaveResult Fail(string detail)
        => new() { IsSuccess = false, DetailText = detail };
}
