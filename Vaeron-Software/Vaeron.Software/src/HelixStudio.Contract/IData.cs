namespace HelixStudio.Contract;

/// <summary>工具要用平台的資料庫</summary>
/// <remarks>工具的畫面實作這個介面，平台建立畫面之後把連線字串塞進來；沒實作就是不需要。</remarks>
public interface IData
{
    /// <summary>平台資料庫的連線字串</summary>
    string Connection { get; set; }
}
