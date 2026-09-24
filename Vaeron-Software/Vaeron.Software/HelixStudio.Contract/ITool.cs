namespace HelixStudio.Contract;

/// <summary>一支工具要平台給它什麼</summary>
/// <remarks>工具的畫面實作這個介面，平台建立畫面之後才把東西塞進來；沒實作就是不需要。</remarks>
public interface ITool
{
    /// <summary>平台的保險箱</summary>
    ISecrets Secrets { get; set; }
}
