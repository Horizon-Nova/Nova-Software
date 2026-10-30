using HelixStudio.Logic;
using HelixStudio.Store;
using Models.HelixStudio;

namespace HelixStudio.Service;

/// <summary>設定這個主題的串接：查出來的列交給運算，運算的結果交回畫面或寫回去</summary>
/// <remarks>不碰畫面；一個使用者動作一支方法，步驟依序寫在裡面。Demo 版工具設定不上鎖，沒有解鎖；也沒有套件庫資料夾那一項。</remarks>
public class SettingsService
{
    private readonly SettingsStore settings = new();

    /// <summary>圖示的線條：啟動時讀一次</summary>
    private readonly Dictionary<string, List<string>> icons = new ShellStore().QueryIconItems();

    #region 設定

    /// <summary>打開設定、選一節：每一項的值、工具底下的每一支、使用紀錄</summary>
    /// <param name="section">現在看的是哪一節</param>
    public SettingsLogic.SettingsView LoadSettings(string section) =>
        SettingsLogic.Settings(settings.QuerySettingsItems(), icons, section);

    /// <summary>平台啟動、改過設定：照現在的設定讀成程式用得到的值</summary>
    public SettingsLogic.SetupView LoadApplied() =>
        SettingsLogic.Applied(settings.QuerySettingsItem(string.Empty));

    /// <summary>點「工具」底下的一支：那支工具的設定畫面在哪一支組件、哪一個型別</summary>
    /// <param name="toolNo">那一支工具的代碼</param>
    public vw_setting? LoadToolSettings(string toolNo) =>
        settings.QuerySettingsItem(toolNo).FirstOrDefault(row => row.type == "tool");

    #endregion

    #region 基本 CRUD 操作

    /// <summary>切一個開關、選一個選項：那一項寫成新的值</summary>
    /// <param name="key">哪一項</param>
    /// <param name="on">開關切成什麼；不是開關是 null</param>
    /// <param name="number">選了哪一個數字；不是數字是 null</param>
    public void LoadChanged(string key, bool? on, int? number) =>
        settings.InsertSettingList(SettingsLogic.Changed(settings.QuerySettingsItem(string.Empty), key, on, number, DateTime.UtcNow));

    /// <summary>回復預設（一項）、回復成資訊部建議設定（每一項）：寫回資訊部建議的值</summary>
    /// <param name="keys">哪幾項；空的就是每一項</param>
    public void LoadReverted(IReadOnlyList<string> keys) =>
        settings.InsertSettingList(SettingsLogic.Reverted(settings.QuerySettingsItem(string.Empty), keys, DateTime.UtcNow));

    #endregion
}
