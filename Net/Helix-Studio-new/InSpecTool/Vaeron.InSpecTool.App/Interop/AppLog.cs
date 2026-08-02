using System.IO;
using System.Text;
using Vaeron.Kernel.Guarding;

namespace Vaeron.InSpecTool.Interop;

/// <summary>中央運作記錄。每一步的真實結果（exit code、檔案 byte 數、例外型別/訊息、IsWindowVisible 探測值）
/// 都寫進來——驗收看 log 檔的確定事實，不是肉眼推斷畫面。呼叫端自行組好單一訊息字串（語意由方法名承載，
/// 不用 action/detail 兩個裸字串參數並排）。</summary>
public static class AppLog
{
    private static readonly object _lock = new();

    public static string LogFilePath { get; } =
        Path.Combine(Path.GetTempPath(), "InSpecTool", "host.log");

    static AppLog()
        => Guard.Run(() => Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!), _ => { });

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Ok(string message) => Write("OK", message);
    public static void Fail(string message) => Write("FAIL", message);

    public static void Error(string action, Exception ex)
        => Write("ERROR", $"{action} | {ex.GetType().Name}: {ex.Message}");

    private static void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.UtcNow:HH:mm:ss.fff} [{level,-5}] {message}";
        lock (_lock)
        {
            Guard.Run(() => File.AppendAllText(LogFilePath, line + Environment.NewLine, Encoding.UTF8), _ => { });
        }
    }
}
