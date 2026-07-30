namespace Vaeron.HelixStudio.Internal.Runtime;

/// <summary>FacadeAuditProxy 攔截到的每一筆呼叫，會分別、獨立地送給每一個已註冊的 IAuditSink——
/// 彼此不互相依賴，一個 Sink 寫入失敗或耗時，不影響其他 Sink 是否寫入、何時完成。</summary>
public interface IAuditSink
{
    public Task CaptureAsync(AuditEntry entry);
}
