using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace UsbPowerGuard;

/// <summary>
/// 透過 powercfg 讀取/設定電源項目。
/// 讀取無需權限；設定 USB 選擇性暫停需要系統管理員（套用時才跳 UAC）。
/// </summary>
public sealed class PowerService
{
    // USB 設定子群組 / USB 選擇性暫停設定（新版 Windows 介面已隱藏此項）
    private const string UsbSubgroup = "2a737441-1930-4402-8d77-b2bebba308a3";
    private const string UsbSelectiveSuspend = "48e6b7a6-50f5-4782-a5d4-53bb8f07e226";

    /// <summary>
    /// 是否啟用現代待機（Connected Standby / S0）。讀登錄檔 CsEnabled，與系統語系無關，
    /// 用來給使用者一句白話結論，不顯示 powercfg 原始輸出。
    /// </summary>
    public bool IsModernStandbyEnabled()
    {
        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Control\Power");
        return key?.GetValue("CsEnabled") is int value && value == 1;
    }

    /// <summary>USB 選擇性暫停目前設定（0 = 已停用，1 = 已啟用；-1 = 讀取失敗）。</summary>
    public (int Ac, int Dc) GetUsbSelectiveSuspend()
    {
        var output = RunRead($"/query SCHEME_CURRENT {UsbSubgroup} {UsbSelectiveSuspend}");
        var values = Regex.Matches(output, "0x([0-9A-Fa-f]{8})")
            .Select(m => Convert.ToInt32(m.Groups[1].Value, 16))
            .ToList();
        if (values.Count < 2) return (-1, -1);
        // 最後兩個分別為「目前交流」「目前直流」設定索引
        return (values[^2], values[^1]);
    }

    /// <summary>
    /// 設定 USB 選擇性暫停（交流+直流），value：0 = 停用、1 = 啟用，並套用為使用中方案。
    /// 以系統管理員權限執行（觸發 UAC）。回傳：true 已套用、false 使用者取消。
    /// </summary>
    public bool SetUsbSelectiveSuspend(int value)
    {
        var cmd =
            $"powercfg /SETACVALUEINDEX SCHEME_CURRENT {UsbSubgroup} {UsbSelectiveSuspend} {value} & " +
            $"powercfg /SETDCVALUEINDEX SCHEME_CURRENT {UsbSubgroup} {UsbSelectiveSuspend} {value} & " +
            $"powercfg /SETACTIVE SCHEME_CURRENT";

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {cmd}",
            UseShellExecute = true,
            Verb = "runas",
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        try
        {
            using var process = Process.Start(psi);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // 使用者在 UAC 取消
            return false;
        }
    }

    private static string RunRead(string powercfgArgs)
    {
        // 透過 cmd 先切 UTF-8 codepage，避免中文輸出亂碼
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c chcp 65001 >nul & powercfg {powercfgArgs}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };

        using var process = Process.Start(psi);
        if (process is null) return string.Empty;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
