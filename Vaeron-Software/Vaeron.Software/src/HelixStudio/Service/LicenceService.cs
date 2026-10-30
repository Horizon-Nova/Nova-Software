using HelixStudio.Logic;
using HelixStudio.Store;

namespace HelixStudio.Service;

/// <summary>授權這個主題的串接：讀出來的授權與機器識別交給運算，運算的結果交回畫面</summary>
/// <remarks>不碰畫面；一個使用者動作一支方法，步驟依序寫在裡面。授權與機器識別打開這一頁時讀一次。</remarks>
public class LicenceService
{
    private readonly LicenceStore licence = new();

    /// <summary>打開這一頁時讀到的授權</summary>
    private readonly LicenceStore.LicenceView paper = new LicenceStore().Licence();

    /// <summary>畫面上露出來的那幾項機器識別：第一次攤這一頁時量一次（平台啟動只問算不算數，不用量）</summary>
    private readonly Lazy<IReadOnlyList<DeviceStore.MarkItem>> shown = new(() => new DeviceStore().Shown());

    /// <summary>圖示的線條：打開這一頁時讀一次</summary>
    private readonly Dictionary<string, List<string>> icons = new ShellStore().QueryIconItems();

    #region 授權

    /// <summary>打開授權這一頁、複製申請碼、打字：申請碼、露出來的機器識別與這一刻的操作</summary>
    /// <param name="moment">這一刻畫面上發生到哪裡</param>
    public LicenceLogic.ActivateView LoadActivate(LicenceLogic.MomentItem moment) =>
        LicenceLogic.Activate(paper, shown.Value, moment, icons);

    /// <summary>套用貼進來的授權：存起來，這一刻再讀一次驗過；回傳算不算數</summary>
    /// <param name="key">貼進來的那一段</param>
    public bool LoadApplied(string key)
    {
        licence.Take(key);

        return LicenceLogic.Allowed(licence.Licence(), DateOnly.FromDateTime(DateTime.Today)) is not null;
    }

    /// <summary>平台啟動：這台電腦的授權算不算數</summary>
    public bool LoadHeld() =>
        LicenceLogic.Allowed(paper, DateOnly.FromDateTime(DateTime.Today)) is not null;

    #endregion
}
