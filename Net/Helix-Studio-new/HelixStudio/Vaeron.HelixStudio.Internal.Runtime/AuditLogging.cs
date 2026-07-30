using System.IO;
using Vaeron.Kernel;
using Vaeron.Kernel.Guarding;

namespace Vaeron.HelixStudio.Internal.Runtime;

/// <summary>IsFailure：Result.Fail 或例外皆為 true，供各 IAuditSink 判斷要標記的嚴重度用，
/// 不在這裡直接放 EventSeverity——那是 EventLog 專案的型別，Runtime 不得反向依賴 Feature。</summary>
public sealed record AuditEntry(string Layer, string Message, Exception? Cause = null, bool IsFailure = false);

public sealed class AuditLogging : IAuditSink
{
    public Task CaptureAsync(AuditEntry entry)
    {
        Log(entry);
        return Task.CompletedTask;
    }

    public void Log(AuditEntry entry)
    {
        var redactedMessage = RedactSecrets(entry.Message);
        var redactedStack = entry.Cause is null ? null : RedactSecrets(entry.Cause.StackTrace);
        var line = $"{DateTimeOffset.UtcNow:O}\t{entry.Layer}\t{redactedMessage}\t{redactedStack}";
        var path = Path.Combine(Paths.Logs, $"{DateTimeOffset.UtcNow:yyyy-MM-dd}.log");

        // 寫入失敗時靜默放棄：稽核日誌本身不得成為新的故障點
        Guard.Run(() => File.AppendAllText(path, line + Environment.NewLine), _ => { });
    }

    private static string? RedactSecrets(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        var result = System.Text.RegularExpressions.Regex.Replace(
            input, @"(?i)\b(password|pwd)\s*=\s*[^;]+", "$1=[已遮罩]",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        result = System.Text.RegularExpressions.Regex.Replace(
            result, @"(?i)\b(username|user id|userid)\s*=\s*[^;]+", "$1=[已遮罩]",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        return result;
    }
}
