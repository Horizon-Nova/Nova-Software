using HelixStudio.Logic;
using HelixStudio.Store;
using Models.HelixStudio;

namespace HelixStudio.Service;

/// <summary>設定這個主題的串接：查出來的列交給運算，運算的結果交回畫面或寫回去</summary>
/// <remarks>不碰畫面；一個使用者動作一支方法，步驟依序寫在裡面。</remarks>
public class SettingsService
{
    private readonly SettingsStore settings = new();

    private readonly LogStore log = new();

    /// <summary>圖示的線條：啟動時讀一次</summary>
    private readonly Dictionary<string, List<string>> icons = new ShellStore().QueryIconItems();

    #region 設定

    /// <summary>打開設定、選一節、每秒倒數：每一項的值、工具底下的每一支、鎖卡或倒數、使用紀錄</summary>
    /// <param name="section">現在看的是哪一節</param>
    /// <param name="until">輸入工具金鑰解開的工具，各自到什麼時刻鎖回</param>
    public SettingsLogic.SettingsView LoadSettings(string section, IReadOnlyDictionary<string, DateTime> until) =>
        SettingsLogic.Settings(settings.QuerySettingsItems(), icons, section, until, DateTime.Now);

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

    /// <summary>平台啟動：還沒有值的路徑類設定，照這台電腦這個帳號的位置填上（種子資料不寫任何一台電腦的路徑）</summary>
    /// <param name="key">哪一項</param>
    /// <param name="path">這台電腦給的路徑</param>
    public void LoadFilled(string key, string path) =>
        settings.InsertSettingList(SettingsLogic.Filled(settings.QuerySettingsItem(string.Empty), key, path, DateTime.UtcNow));

    /// <summary>輸入工具金鑰解開一支工具的設定：配得上就記一筆解鎖；回傳配不上的原因（配得上是空字串）與解開幾分鐘</summary>
    /// <param name="toolNo">那一支工具的代碼</param>
    /// <param name="code">貼進來的工具金鑰</param>
    public (string Fault, int Minutes) LoadUnlocked(string toolNo, string code)
    {
        // 驗金鑰：簽章、這台電腦、到期日、有沒有這支工具
        var fault = new LicenceStore().Fits(code, toolNo);
        var tool = settings.QuerySettingsItem(toolNo).FirstOrDefault(row => row.type == "tool");
        var minutes = tool?.unlock_minutes ?? 0;

        // 配得上：記一筆解鎖
        log.InsertEventLogList([.. new[] { fault }.Where(one => one.Length == 0).Select(_ => new event_log
        {
            type = "unlock",
            tool_no = toolNo,
            account = Environment.UserName,
            function_name = $"{nameof(SettingsService)}.{nameof(LoadUnlocked)}",
            message = $"用工具金鑰解開 {tool?.name} 的設定 {minutes} 分鐘",
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow,
        })]);

        return (fault, minutes);
    }

    #endregion
}
