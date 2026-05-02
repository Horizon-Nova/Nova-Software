namespace HorizonNova.HelixStudio.ModuleSdk;

/// <summary>
/// 模組設定存取介面，由主程式殼層透過 <see cref="IShellContext.Settings"/> 提供；數值由殼層持久化於工作階段之間。
/// </summary>
public interface IModuleSettings
{
    /// <summary>依鍵名查詢字串設定；無資料時回傳 <paramref name="fallback"/>。</summary>
    string QueryString(string key, string fallback = "");

    /// <summary>依鍵名查詢 32 位元整數設定；無資料時回傳 <paramref name="fallback"/>。</summary>
    int QueryInt32(string key, int fallback = 0);

    /// <summary>依鍵名查詢布林設定；無資料時回傳 <paramref name="fallback"/>。</summary>
    bool QueryBoolean(string key, bool fallback = false);

    /// <summary>依鍵名查詢倍精度浮點數設定；無資料時回傳 <paramref name="fallback"/>。</summary>
    double QueryDouble(string key, double fallback = 0.0);

    /// <summary>寫入字串設定並回傳儲存結果。</summary>
    ModuleSettingSaveResult SaveString(string key, string? value);

    /// <summary>寫入 32 位元整數設定並回傳儲存結果。</summary>
    ModuleSettingSaveResult SaveInt32(string key, int value);

    /// <summary>寫入布林設定並回傳儲存結果。</summary>
    ModuleSettingSaveResult SaveBoolean(string key, bool value);

    /// <summary>寫入倍精度浮點數設定並回傳儲存結果。</summary>
    ModuleSettingSaveResult SaveDouble(string key, double value);
}
