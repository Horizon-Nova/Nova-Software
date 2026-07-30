using Microsoft.Win32;
using Vaeron.Kernel;

namespace Vaeron.HelixStudio.Settings.Internal;

/// <summary>開機自動啟動登錄機碼讀寫</summary>
internal sealed class StartupRegistrationStore
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Vaeron.HelixStudio";

    public Result ApplyStartWithWindows(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null) return Result.Fail(ResultKind.Unavailable, "找不到登錄機碼，無法設定開機自動啟動");

            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return Result.Fail(ResultKind.Unavailable, "無法取得執行檔路徑，開機自動啟動未設定");
                key.SetValue(ValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            return new Result { Kind = ResultKind.Success, Message = "成功" };
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return Result.Fail(ResultKind.Unavailable, "沒有足夠權限設定開機自動啟動");
        }
    }

    /// <summary>查詢登錄機碼是否指向目前執行檔路徑</summary>
    public bool QueryIsRegisteredForCurrentPath()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return false;

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var value = key?.GetValue(ValueName) as string;
        return value == $"\"{exePath}\"";
    }
}
