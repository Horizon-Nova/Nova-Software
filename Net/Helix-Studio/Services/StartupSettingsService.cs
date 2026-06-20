using Microsoft.Win32;

namespace Helix_Studio.Services;

public sealed class StartupSettingsService
{
    private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string RegistryValue = "HelixStudio";

    public void ApplyStartWithWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, writable: true);
        if (key is null) return;
        if (enabled)
            key.SetValue(RegistryValue, $"\"{System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName}\"");
        else
            key.DeleteValue(RegistryValue, throwOnMissingValue: false);
    }

    public bool IsStartWithWindowsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey);
        return key?.GetValue(RegistryValue) is not null;
    }
}
