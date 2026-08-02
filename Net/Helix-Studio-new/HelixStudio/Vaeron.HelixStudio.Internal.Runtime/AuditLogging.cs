using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Vaeron.Kernel;
using Vaeron.Kernel.Guarding;

namespace Vaeron.HelixStudio.Internal.Runtime;

/// <summary>IsFailure：Result.Fail 或例外皆為 true，供各 IAuditSink 判斷要標記的嚴重度用，
/// 不在這裡直接放 EventSeverity——那是 EventLog 專案的型別，Runtime 不得反向依賴 Feature。</summary>
public sealed record AuditEntry(string Layer, string Message, Exception? Cause = null, bool IsFailure = false);

public sealed class AuditLogging : IAuditSink
{
    private static bool _directoryLocked;

    public Task CaptureAsync(AuditEntry entry)
    {
        Log(entry);
        return Task.CompletedTask;
    }

    public void Log(AuditEntry entry)
    {
        EnsureDirectoryLocked();

        var redactedMessage = RedactSecrets(entry.Message);
        var redactedStack = entry.Cause is null ? null : RedactSecrets(entry.Cause.StackTrace);
        var line = $"{DateTimeOffset.UtcNow:O}\t{entry.Layer}\t{redactedMessage}\t{redactedStack}";
        var path = Path.Combine(Paths.Logs, $"{DateTimeOffset.UtcNow:yyyy-MM-dd}.log");

        // 寫入失敗時靜默放棄：稽核日誌本身不得成為新的故障點
        Guard.Run(() => File.AppendAllText(path, line + Environment.NewLine), _ => { });
    }

    // 內容機密、不得任意開啟：移除資料夾繼承來的廣泛權限，只留目前 Windows 使用者本身可讀寫，
    // 其他本機帳號（含其他一般使用者）無法再從檔案總管或記事本直接開啟。只需鎖一次資料夾本身——
    // 之後在這個資料夾底下新建的每日 log 檔案會沿用資料夾的權限繼承設定，不必逐檔另外上鎖。
    // 寫入失敗時靜默放棄，理由同上：稽核日誌本身不得成為新的故障點。
    private static void EnsureDirectoryLocked()
    {
        if (_directoryLocked) return;
        _directoryLocked = true;

        Guard.Run(() =>
        {
            var directory = new DirectoryInfo(Paths.Logs);
            var security = directory.GetAccessControl();
            security.SetAccessRuleProtection(true, false);
            foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(NTAccount)))
                security.RemoveAccessRule(rule);
            security.AddAccessRule(new FileSystemAccessRule(
                WindowsIdentity.GetCurrent().User!,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            directory.SetAccessControl(security);
        }, _ => { });
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
