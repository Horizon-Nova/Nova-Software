namespace Vaeron.SharedKernel;

public sealed class AuditLogging(string logDirectory)
{
    public void Log(string layer, string message, Exception? ex = null)
    {
        var redactedMessage = RedactSecrets(message);
        var redactedStack = ex is null ? null : RedactSecrets(ex.StackTrace);
        var line = $"{DateTimeOffset.UtcNow:O}\t{layer}\t{redactedMessage}\t{redactedStack}";

        var path = Path.Combine(logDirectory, $"{DateTimeOffset.UtcNow:yyyy-MM-dd}.log");
        try
        {
            File.AppendAllText(path, line + Environment.NewLine);
        }
        catch (IOException)
        {
            // 寫日誌失敗不能讓應用程式崩潰，靜默放棄這一筆——Log 是 fire-and-forget，
            // 沒有回傳值可用來反映失敗，刻意選擇吞掉而非拋出。
        }
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
