namespace Vaeron.HelixStudio.Shell;

/// <summary>一個導覽目的地在殼層裡的版面需求。</summary>
/// <remarks>系統助理自己帶軟體清單，不需要殼層的左側資訊欄；新增畫面時在此加一列。</remarks>
public sealed record ScreenLayout(string NavId, bool ShowsSidePanel);
