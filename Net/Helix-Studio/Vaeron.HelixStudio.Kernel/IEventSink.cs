namespace Vaeron.HelixStudio.Kernel;

/// <summary>功能單元寫出事件的唯一出口。實作者負責記錄與呈現，寫出者不知道也不問去了哪裡。</summary>
/// <remarks>放在 Kernel 而非任一功能組件：功能之間不得互相引用，事件是它們共同的下游。</remarks>
public interface IEventSink
{
    /// <summary>Level 的值域比照事件日誌的嚴重度：info／warning／error。</summary>
    public void Write(string level, string text);
}
