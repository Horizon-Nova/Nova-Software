using HelixStudio.Logic;
using HelixStudio.Store;

namespace HelixStudio.Service;

/// <summary>指令面板這個主題的串接：查出來的列交給運算，運算的結果交回畫面</summary>
/// <remarks>不碰畫面；一個使用者動作一支方法，步驟依序寫在裡面。選過的工具記成搜尋字是主視窗的 ShellService 做。</remarks>
public class PaletteService
{
    private readonly PaletteStore palette = new();

    /// <summary>圖示的線條：啟動時讀一次</summary>
    private readonly Dictionary<string, List<string>> icons = new ShellStore().QueryIconItems();

    #region 指令面板

    /// <summary>打開面板、打字：每一支工具與搜尋過的字，照打的字攤成面板</summary>
    /// <param name="keyword">面板上打的字</param>
    public PaletteLogic.PaletteView LoadPalette(string keyword) =>
        PaletteLogic.Palette(palette.QueryPaletteItems(), icons, keyword);

    #endregion
}
