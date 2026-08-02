using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Vaeron.HelixStudio.Settings.Internal.Models;
using Vaeron.Kernel;

namespace Vaeron.HelixStudio.Settings.Internal;

internal sealed record SleepTimeoutCommand(int AcMinutes, int BatteryMinutes);

internal sealed record AcDcMinutes(int Ac, int Dc);

/// <summary>電源 8 欄現況查詢結果。逐欄可為 null——單欄查詢失敗不影響其餘欄位，
/// 由呼叫方（SettingsService）決定 null 時要不要退回資料庫快取。</summary>
internal sealed record PowerStateSnapshot
{
    public string? PowerMode { get; init; }
    public int? AcSleepMinutes { get; init; }
    public int? BatterySleepMinutes { get; init; }
    public int? ScreenTimeoutMinutes { get; init; }
    public bool? HibernateEnabled { get; init; }
    public bool? FastStartupEnabled { get; init; }
    public string? PowerButtonAction { get; init; }
    public string? LidCloseAction { get; init; }
}

/// <summary>電源設定的實際系統套用點：透過 powercfg.exe 行程呼叫（電源方案／睡眠與螢幕逾時／按鈕動作／休眠）
/// 與登錄機碼（快速啟動），把畫面上的選擇變成真的 Windows 電源設定；另外提供「立即執行」電源動作
/// （供闔螢幕顯示關閉選單使用者選定後呼叫），跟上面「設定政策」是兩種不同性質的操作，見下方 Region。</summary>
internal sealed class PowerManagementStore
{
    /// <summary>SCHEME_MAX／SCHEME_MIN 是以「省電程度」命名，不是效能高低——經 powercfg 實測驗證：
    /// SCHEME_MAX＝最大省電（省電模式）、SCHEME_MIN＝最小省電（高效能模式），與字面直覺相反，別搞反。</summary>
    private static readonly Dictionary<string, string> SchemeAliases = new()
    {
        ["省電"] = "SCHEME_MAX",
        ["平衡"] = "SCHEME_BALANCED",
        ["高效能"] = "SCHEME_MIN",
    };

    /// <summary>別名對應的真實 GUID——查詢現況時 powercfg 只回傳 GUID，靠這份對照表反查別名。
    /// 三組 GUID 已於本機以 `powercfg /list` 實測核對，非文件推導。</summary>
    private static readonly Dictionary<string, string> SchemeGuids = new()
    {
        ["SCHEME_BALANCED"] = "381b4222-f694-41f0-9685-ff5bb260df2e",
        ["SCHEME_MAX"] = "a1841308-3541-4fab-bc81-f71556f20b4a",
        ["SCHEME_MIN"] = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
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

    /// <summary>
    /// 電源 8 欄現況一律直接查 Windows，不查資料庫——離線真實鏡像設計，取代舊版單向重推。
    /// cachedRow 只在 LidCloseAction 遇到系統值無法反推時（見下方說明）當備援依據，不影響其餘 7 欄。
    /// </summary>
    public async Task<PowerStateSnapshot> QueryCurrentStateAsync(AppSettingsRow cachedRow)
    {
        var scheme = await QueryActivePowerModeAsync();
        var sleep = await QueryAcDcMinutesAsync("SUB_SLEEP", "STANDBYIDLE");
        var screen = await QueryAcDcMinutesAsync("SUB_VIDEO", "VIDEOIDLE");
        var hibernate = await QueryHibernateEnabledAsync();
        var fastStartup = QueryFastStartupEnabled();
        var powerButton = await QueryButtonActionAsync("PBUTTONACTION", PowerButtonActionValues);
        var lidClose = await QueryLidCloseActionAsync(cachedRow.lid_close_action);

        return new PowerStateSnapshot
        {
            PowerMode = scheme.IsSuccess ? scheme.Value : null,
            AcSleepMinutes = sleep.IsSuccess ? sleep.Value!.Ac : null,
            BatterySleepMinutes = sleep.IsSuccess ? sleep.Value!.Dc : null,
            ScreenTimeoutMinutes = screen.IsSuccess ? screen.Value!.Ac : null,
            HibernateEnabled = hibernate.IsSuccess ? hibernate.Value : null,
            FastStartupEnabled = fastStartup.IsSuccess ? fastStartup.Value : null,
            PowerButtonAction = powerButton.IsSuccess ? powerButton.Value : null,
            LidCloseAction = lidClose.IsSuccess ? lidClose.Value : null,
        };
    }

    private static async Task<Result<string>> QueryActivePowerModeAsync()
    {
        var output = await RunPowercfgQueryAsync("/getactivescheme");
        if (!output.IsSuccess || output.Value is null) return Result<string>.Fail(output.Kind, output.Message);

        var match = Regex.Match(output.Value, @"GUID[:：]\s*([0-9a-fA-F-]{36})");
        if (!match.Success) return Result<string>.Fail(ResultKind.InfrastructureError, "無法解析目前電源模式");

        var guid = match.Groups[1].Value;
        var name = SchemeAliases.FirstOrDefault(kv => SchemeGuids[kv.Value].Equals(guid, StringComparison.OrdinalIgnoreCase)).Key;
        return name is null ? Result<string>.Fail(ResultKind.InfrastructureError, "目前使用非已知電源模式") : Result<string>.Ok(name);
    }

    /// <summary>睡眠／螢幕逾時共用：powercfg 回傳秒數索引，換算成分鐘。AC/DC 兩個英文字母
    /// 在繁中在地化輸出裡未被翻譯，靠它們比對比逐行比對中文標籤字串更不脆弱（已實機驗證）。</summary>
    private static async Task<Result<AcDcMinutes>> QueryAcDcMinutesAsync(string subgroupAlias, string settingAlias)
    {
        var output = await RunPowercfgQueryAsync($"/q SCHEME_CURRENT {subgroupAlias} {settingAlias}");
        if (!output.IsSuccess || output.Value is null) return Result<AcDcMinutes>.Fail(output.Kind, output.Message);

        var ac = Regex.Match(output.Value, @"AC.*?0x([0-9a-fA-F]+)");
        var dc = Regex.Match(output.Value, @"DC.*?0x([0-9a-fA-F]+)");
        if (!ac.Success || !dc.Success) return Result<AcDcMinutes>.Fail(ResultKind.InfrastructureError, "無法解析電源設定索引");

        var acSeconds = Convert.ToInt32(ac.Groups[1].Value, 16);
        var dcSeconds = Convert.ToInt32(dc.Groups[1].Value, 16);
        return Result<AcDcMinutes>.Ok(new AcDcMinutes(acSeconds / 60, dcSeconds / 60));
    }

    /// <summary>電源按鈕動作是隱藏電源設定，必須用 /qh（查隱藏設定）——實機驗證 /q 在本機只回傳空殼。</summary>
    private static async Task<Result<string>> QueryButtonActionAsync(string settingAlias, Dictionary<string, int> actionValues)
    {
        var output = await RunPowercfgQueryAsync($"/qh SCHEME_CURRENT SUB_BUTTONS {settingAlias}");
        if (!output.IsSuccess || output.Value is null) return Result<string>.Fail(output.Kind, output.Message);

        var ac = Regex.Match(output.Value, @"AC.*?0x([0-9a-fA-F]+)");
        if (!ac.Success) return Result<string>.Fail(ResultKind.InfrastructureError, "無法解析按鈕動作設定");

        var value = Convert.ToInt32(ac.Groups[1].Value, 16);
        var label = actionValues.FirstOrDefault(kv => kv.Value == value).Key;
        return label is null ? Result<string>.Fail(ResultKind.InfrastructureError, "非已知按鈕動作") : Result<string>.Ok(label);
    }

    /// <summary>闔蓋動作比電源按鈕多一個陷阱：「不執行動作」與「顯示關閉選單」在系統層都寫 LIDACTION=0，
    /// 系統真實值天生無法反推是這兩者的哪一個（App 自己用另一套機制 GUID_LIDSWITCH_STATE_CHANGE 攔截闔蓋，
    /// Windows 本身完全不知道「顯示關閉選單」這個概念）。查到 0 時沿用資料庫快取裡這兩者之一的既有選擇，
    /// 不強行判定成「不執行動作」抹掉使用者原本的「顯示關閉選單」設定。</summary>
    private static async Task<Result<string>> QueryLidCloseActionAsync(string cachedFallback)
    {
        var output = await RunPowercfgQueryAsync("/qh SCHEME_CURRENT SUB_BUTTONS LIDACTION");
        if (!output.IsSuccess || output.Value is null) return Result<string>.Fail(output.Kind, output.Message);

        var ac = Regex.Match(output.Value, @"AC.*?0x([0-9a-fA-F]+)");
        if (!ac.Success) return Result<string>.Fail(ResultKind.InfrastructureError, "無法解析闔蓋動作設定");

        var value = Convert.ToInt32(ac.Groups[1].Value, 16);
        if (value == 0)
            return Result<string>.Ok(cachedFallback is "顯示關閉選單" or "不執行動作" ? cachedFallback : "不執行動作");

        var label = LidCloseActionValues.FirstOrDefault(kv => kv.Value == value).Key;
        return label is null ? Result<string>.Fail(ResultKind.InfrastructureError, "非已知闔蓋動作") : Result<string>.Ok(label);
    }

    private static Result<bool> QueryFastStartupEnabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power");
            if (key is null) return Result<bool>.Fail(ResultKind.Unavailable, "找不到快速啟動的系統設定機碼");

            var value = key.GetValue("HiberbootEnabled");
            return value is int i ? Result<bool>.Ok(i != 0) : Result<bool>.Fail(ResultKind.Unavailable, "機碼值型別不符預期");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return Result<bool>.Fail(ResultKind.Unavailable, "沒有足夠權限讀取快速啟動設定");
        }
    }

    /// <summary>休眠是否啟用：不能用 hiberfil.sys 是否存在判斷（已實機驗證兩者會不一致，
    /// 可能因非系統管理員權限看不到受保護系統檔案）。改用 /a 輸出——「有」的段落列出「休眠」
    /// 即已啟用，出現在「缺乏」段落（原因含「已停用」）則是停用，與寫入用同一支工具查詢，資料來源一致。</summary>
    private static async Task<Result<bool>> QueryHibernateEnabledAsync()
    {
        var output = await RunPowercfgQueryAsync("/a");
        if (!output.IsSuccess || output.Value is null) return Result<bool>.Fail(output.Kind, output.Message);

        var availableSection = output.Value.Split("缺乏", 2)[0];
        return Result<bool>.Ok(availableSection.Contains("休眠"));
    }

    private static async Task<Result<string>> RunPowercfgQueryAsync(string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo("powercfg.exe", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };
            using var process = Process.Start(startInfo);
            if (process is null) return Result<string>.Fail(ResultKind.InfrastructureError, "無法啟動 powercfg");

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0) return Result<string>.Fail(ResultKind.InfrastructureError, "powercfg 查詢失敗");

            return Result<string>.Ok(output);
        }
        catch (Win32Exception)
        {
            return Result<string>.Fail(ResultKind.InfrastructureError, "找不到 powercfg 工具");
        }
    }

    public async Task<Result> ApplyPowerModeAsync(string mode)
    {
        if (!SchemeAliases.TryGetValue(mode, out var alias)) return Ok();
        return await RunPowercfgAsync($"/setactive {alias}", "電源模式");
    }

    public async Task<Result> ApplySleepTimeoutAsync(SleepTimeoutCommand command)
    {
        var ac = await RunPowercfgAsync($"/change standby-timeout-ac {command.AcMinutes}", "插電時，進入睡眠");
        if (!ac.IsSuccess) return ac;
        return await RunPowercfgAsync($"/change standby-timeout-dc {command.BatteryMinutes}", "電池電力時，進入睡眠");
    }

    public async Task<Result> ApplyScreenTimeoutAsync(int minutes)
    {
        var ac = await RunPowercfgAsync($"/change monitor-timeout-ac {minutes}", "螢幕逾時");
        if (!ac.IsSuccess) return ac;
        return await RunPowercfgAsync($"/change monitor-timeout-dc {minutes}", "螢幕逾時");
    }

    public async Task<Result> ApplyHibernateAsync(bool enabled)
        => await RunPowercfgAsync($"/hibernate {(enabled ? "on" : "off")}", "休眠（Hibernate）");

    public async Task<Result> ApplyPowerButtonActionAsync(string action)
        => await ApplyButtonActionAsync("PBUTTONACTION", PowerButtonActionValues, action, "按下電源按鈕時");

    public async Task<Result> ApplyLidCloseActionAsync(string action)
        => await ApplyButtonActionAsync("LIDACTION", LidCloseActionValues, action, "闔上筆電螢幕時");

    /// <summary>快速啟動需寫入 HKLM 機碼，非系統管理員權限會失敗——這裡不是 async I/O（純登錄機碼操作，同 StartupRegistrationStore）。</summary>
    public Result ApplyFastStartup(bool enabled)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", writable: true);
            if (key is null) return Result.Fail(ResultKind.Unavailable, "找不到快速啟動的系統設定機碼");
            key.SetValue("HiberbootEnabled", enabled ? 1 : 0, RegistryValueKind.DWord);
            return Ok();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return Result.Fail(ResultKind.Unavailable, "沒有足夠權限設定快速啟動（需要系統管理員權限）");
        }
    }

    private async Task<Result> ApplyButtonActionAsync(string settingAlias, Dictionary<string, int> actionValues, string action, string fieldName)
    {
        if (!actionValues.TryGetValue(action, out var value))
            return new Result { Kind = ResultKind.Success, Message = $"已儲存，但「{action}」沒有對應的系統動作，不會實際套用" };

        var ac = await RunPowercfgAsync($"/setacvalueindex SCHEME_CURRENT SUB_BUTTONS {settingAlias} {value}", fieldName);
        if (!ac.IsSuccess) return ac;
        var dc = await RunPowercfgAsync($"/setdcvalueindex SCHEME_CURRENT SUB_BUTTONS {settingAlias} {value}", fieldName);
        if (!dc.IsSuccess) return dc;
        return await RunPowercfgAsync("/setactive SCHEME_CURRENT", fieldName);
    }

    #region 立即執行電源動作（闔螢幕顯示關閉選單，使用者選定後呼叫；跟上面「設定政策」不同，這裡是馬上執行）

    public Result ApplySleepNow() => ExecuteSuspend(hibernate: false, "睡眠");

    public Result ApplyHibernateNow() => ExecuteSuspend(hibernate: true, "休眠");

    public async Task<Result> ApplyShutdownNowAsync()
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
            if (process is null) return Result.Fail(ResultKind.InfrastructureError, "關機失敗：無法啟動 shutdown");

            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                return Result.Fail(ResultKind.InfrastructureError, $"關機失敗：{(string.IsNullOrWhiteSpace(error) ? "系統拒絕此變更" : error.Trim())}");

            return Ok();
        }
        catch (Win32Exception)
        {
            return Result.Fail(ResultKind.InfrastructureError, "關機失敗：找不到 shutdown 工具");
        }
    }

    private static Result ExecuteSuspend(bool hibernate, string fieldName)
    {
        var success = SetSuspendState(hibernate, false, false);
        return success ? Ok() : Result.Fail(ResultKind.InfrastructureError, $"{fieldName}失敗：系統拒絕此變更");
    }

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    #endregion

    private static Result Ok() => new() { Kind = ResultKind.Success, Message = "成功" };

    private static async Task<Result> RunPowercfgAsync(string arguments, string fieldName)
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
            if (process is null) return Result.Fail(ResultKind.InfrastructureError, $"{fieldName}設定失敗：無法啟動 powercfg");

            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                return Result.Fail(ResultKind.InfrastructureError, $"{fieldName}設定失敗：{(string.IsNullOrWhiteSpace(error) ? "系統拒絕此變更" : error.Trim())}");

            return Ok();
        }
        catch (Win32Exception)
        {
            return Result.Fail(ResultKind.InfrastructureError, $"{fieldName}設定失敗：找不到 powercfg 工具");
        }
    }
}
