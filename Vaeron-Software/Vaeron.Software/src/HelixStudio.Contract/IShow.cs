namespace HelixStudio.Contract;

/// <summary>工具要把自己那一頁叫到前面</summary>
/// <remarks>工具的畫面實作這個介面，平台建立畫面之後把動作塞進來；呼叫它，平台就把主視窗叫回前面（縮小的還原）並開這支工具。在背景跑的工具（例如程式監控）用它從自己跳出來的小視窗回到監控台。</remarks>
public interface IShow
{
    /// <summary>把主視窗叫回前面並開這支工具</summary>
    Action Show { get; set; }
}
