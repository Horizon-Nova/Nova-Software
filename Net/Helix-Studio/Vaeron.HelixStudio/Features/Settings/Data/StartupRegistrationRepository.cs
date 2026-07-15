using Microsoft.Win32;

namespace Vaeron.HelixStudio.Features.Settings.Data;

/// <summary>「開機時自動啟動本軟體」的唯一寫入點——寫入/移除目前使用者的
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Run 機碼，這是使用者層級的自啟動註冊，
/// 不需要系統管理員權限，跟系統服務／排程工作是不同機制。失敗（機碼被政策鎖住等）是
/// 使用者可理解的業務事實，不是基礎設施故障，攔截判準同 repositories.md #2。</summary>
public sealed class StartupRegistrationRepository
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Vaeron.HelixStudio";

    public (bool success, string message) ApplyStartWithWindows(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null) return (false, "找不到登錄機碼，無法設定開機自動啟動");

            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return (false, "無法取得執行檔路徑，開機自動啟動未設定");
                key.SetValue(ValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            return (true, "成功");
        }
        catch (System.Security.SecurityException)
        {
            return (false, "沒有足夠權限設定開機自動啟動");
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "沒有足夠權限設定開機自動啟動");
        }
    }
}
