using System.IO;
using System.Text;

namespace InSpecHookDLLHost;

/// <summary>
/// 中央運作記錄（middleware）。使用者提議：把所有東西——含錯誤、運作訊息——自動漏進 log，
/// 驗證就從「截圖用肉眼推斷」變成「讀 log 檔的確定事實」（工程憲法：驗收＝外部系統實際執行後告訴我結果）。
/// 每一步的真實結果（exit code、檔案 byte 數、例外型別/訊息、IsWindowVisible 探測值）都寫進來。
/// 寫檔＋即時事件雙軌：檔案供事後驗證，事件供 UI 即時面板。
/// </summary>
public static class AppLog
{
    private static readonly object _lock = new();

    public static string LogFilePath { get; } =
        Path.Combine(Path.GetTempPath(), "InSpecHost", "host.log");

    /// <summary>每寫一行就觸發，供 UI 即時面板訂閱。</summary>
    public static event Action<string>? LineAppended;

    static AppLog()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!); }
        catch (IOException) { /* 目錄建立失敗就只走事件、不寫檔，不讓 log 本身把 app 弄掛 */ }
    }

    public static void Info(string action, string detail = "") => Write("INFO", action, detail);
    public static void Warn(string action, string detail = "") => Write("WARN", action, detail);

    public static void Error(string action, Exception ex)
        => Write("ERROR", action, $"{ex.GetType().Name}: {ex.Message}");

    /// <summary>記錄一個操作的真實結果：ok + 附帶事實（不是「應該成功」而是實際回傳值）。</summary>
    public static void Result(string action, bool ok, string detail = "")
        => Write(ok ? "OK" : "FAIL", action, detail);

    private static void Write(string level, string action, string detail)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} [{level,-5}] {action}"
                 + (detail.Length > 0 ? $" | {detail}" : "");
        lock (_lock)
        {
            try { File.AppendAllText(LogFilePath, line + Environment.NewLine, Encoding.UTF8); }
            catch (IOException) { /* log 寫檔失敗無處可報（fire-and-forget），刻意吞掉、仍走事件 */ }
        }
        LineAppended?.Invoke(line);
    }
}
