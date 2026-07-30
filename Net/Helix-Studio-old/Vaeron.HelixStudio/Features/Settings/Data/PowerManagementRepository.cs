using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Vaeron.HelixStudio.Features.Settings.Data;

/// <summary>電源設定的實際系統套用點：透過 powercfg.exe 行程呼叫（電源方案／睡眠與螢幕逾時／按鈕動作／休眠）
/// 與登錄機碼（快速啟動），把畫面上的選擇變成真的 Windows 電源設定；另外提供「立即執行」電源動作
/// （供闔螢幕顯示關閉選單使用者選定後呼叫），跟上面「設定政策」是兩種不同性質的操作，見下方 Region。</summary>
public sealed class PowerManagementRepository
{
    /// <summary>SCHEME_MAX／SCHEME_MIN 是以「省電程度」命名，不是效能高低——經 powercfg 實測驗證：
    /// SCHEME_MAX＝最大省電（省電模式）、SCHEME_MIN＝最小省電（高效能模式），與字面直覺相反，別搞反。</summary>
    private static readonly Dictionary<string, string> SchemeAliases = new()
    {
        ["省電"] = "SCHEME_MAX",
        ["平衡"] = "SCHEME_BALANCED",
        ["高效能"] = "SCHEME_MIN",
    };

    /// <summary>電源按鈕：「顯示關閉選單」故意不列——查證微軟官方 Power Setting GUIDs 文件與社群討論，
    /// Windows 沒有公開 API 能攔截「電源鍵被按下」這個動作本身（PBUTTONACTION 只能設定按下去要執行的政策，
    /// 不是事件通知），技術上做不到，見 auto memory 的 power-button-lid-action-feasibility 記錄。</summary>
    private static readonly Dictionary<string, int> PowerButtonActionValues = new()
    {
        ["不執行動作"] = 0,
        ["睡眠"] = 1,
        ["休眠"] = 2,
        ["關機"] = 3,
    };

    /// <summary>闔螢幕：「顯示關閉選單」對應到「不執行動作」（0）——讓 Windows 閤蓋時真的不做任何事，
    /// 改由 App 自己透過 GUID_LIDSWITCH_STATE_CHANGE（MainWindow 攔截）偵測閤蓋、跳出自訂選單。
    /// 這條路對闔螢幕成立、對電源按鈕不成立，是因為 GUID_LIDSWITCH_STATE_CHANGE 是微軟官方文件裡
    /// 真實存在的通知 GUID，電源按鈕沒有對應物，兩者不對稱，不能套同一套判斷。</summary>
    private static readonly Dictionary<string, int> LidCloseActionValues = new()
    {
        ["不執行動作"] = 0,
        ["睡眠"] = 1,
        ["休眠"] = 2,
        ["關機"] = 3,
        ["顯示關閉選單"] = 0,
    };

    public async Task<(bool success, string message)> ApplyPowerModeAsync(string mode)
    {
        if (!SchemeAliases.TryGetValue(mode, out var alias)) return (true, "成功");
        return await RunPowercfgAsync($"/setactive {alias}", "電源模式");
    }

    public async Task<(bool success, string message)> ApplySleepTimeoutAsync(int acMinutes, int batteryMinutes)
    {
        var (acSuccess, acMessage) = await RunPowercfgAsync($"/change standby-timeout-ac {acMinutes}", "插電時，進入睡眠");
        if (!acSuccess) return (acSuccess, acMessage);
        return await RunPowercfgAsync($"/change standby-timeout-dc {batteryMinutes}", "電池電力時，進入睡眠");
    }

    public async Task<(bool success, string message)> ApplyScreenTimeoutAsync(int minutes)
    {
        var (acSuccess, acMessage) = await RunPowercfgAsync($"/change monitor-timeout-ac {minutes}", "螢幕逾時");
        if (!acSuccess) return (acSuccess, acMessage);
        return await RunPowercfgAsync($"/change monitor-timeout-dc {minutes}", "螢幕逾時");
    }

    public async Task<(bool success, string message)> ApplyHibernateAsync(bool enabled)
        => await RunPowercfgAsync($"/hibernate {(enabled ? "on" : "off")}", "休眠（Hibernate）");

    public async Task<(bool success, string message)> ApplyPowerButtonActionAsync(string action)
        => await ApplyButtonActionAsync("PBUTTONACTION", PowerButtonActionValues, action, "按下電源按鈕時");

    public async Task<(bool success, string message)> ApplyLidCloseActionAsync(string action)
        => await ApplyButtonActionAsync("LIDACTION", LidCloseActionValues, action, "闔上筆電螢幕時");

    /// <summary>快速啟動需寫入 HKLM 機碼，非系統管理員權限會失敗——這裡不是 async I/O（純登錄機碼操作，同 StartupRegistrationRepository），
    /// 呼叫端仍以 await 消化回傳的 (bool, string)，維持與其餘電源方法一致的簽名。</summary>
    public (bool success, string message) ApplyFastStartup(bool enabled)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", writable: true);
            if (key is null) return (false, "找不到快速啟動的系統設定機碼");
            key.SetValue("HiberbootEnabled", enabled ? 1 : 0, RegistryValueKind.DWord);
            return (true, "成功");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return (false, "沒有足夠權限設定快速啟動（需要系統管理員權限）");
        }
    }

    private async Task<(bool success, string message)> ApplyButtonActionAsync(string settingAlias, Dictionary<string, int> actionValues, string action, string fieldName)
    {
        if (!actionValues.TryGetValue(action, out var value))
            return (true, $"已儲存，但「{action}」沒有對應的系統動作，不會實際套用");

        var (acSuccess, acMessage) = await RunPowercfgAsync($"/setacvalueindex SCHEME_CURRENT SUB_BUTTONS {settingAlias} {value}", fieldName);
        if (!acSuccess) return (acSuccess, acMessage);
        var (dcSuccess, dcMessage) = await RunPowercfgAsync($"/setdcvalueindex SCHEME_CURRENT SUB_BUTTONS {settingAlias} {value}", fieldName);
        if (!dcSuccess) return (dcSuccess, dcMessage);
        return await RunPowercfgAsync("/setactive SCHEME_CURRENT", fieldName);
    }

    #region 立即執行電源動作（闔螢幕顯示關閉選單，使用者選定後呼叫；跟上面「設定政策」不同，這裡是馬上執行）

    public (bool success, string message) ApplySleepNow()
        => ExecuteSuspend(hibernate: false, "睡眠");

    public (bool success, string message) ApplyHibernateNow()
        => ExecuteSuspend(hibernate: true, "休眠");

    public async Task<(bool success, string message)> ApplyShutdownNowAsync()
    {
        try
        {
            var startInfo = new ProcessStartInfo("shutdown.exe", "/s /t 0")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(startInfo);
            if (process is null) return (false, "關機失敗：無法啟動 shutdown");

            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                return (false, $"關機失敗：{(string.IsNullOrWhiteSpace(error) ? "系統拒絕此變更" : error.Trim())}");

            return (true, "成功");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return (false, "關機失敗：找不到 shutdown 工具");
        }
    }

    private static (bool success, string message) ExecuteSuspend(bool hibernate, string fieldName)
    {
        var success = SetSuspendState(hibernate, false, false);
        return success ? (true, "成功") : (false, $"{fieldName}失敗：系統拒絕此變更");
    }

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    #endregion

    private static async Task<(bool success, string message)> RunPowercfgAsync(string arguments, string fieldName)
    {
        try
        {
            var startInfo = new ProcessStartInfo("powercfg.exe", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(startInfo);
            if (process is null) return (false, $"{fieldName}設定失敗：無法啟動 powercfg");

            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                return (false, $"{fieldName}設定失敗：{(string.IsNullOrWhiteSpace(error) ? "系統拒絕此變更" : error.Trim())}");

            return (true, "成功");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return (false, $"{fieldName}設定失敗：找不到 powercfg 工具");
        }
    }
}
