using Vaeron.HelixStudio.Internal.Runtime.AppRuntime;
using Vaeron.HelixStudio.Settings.Internal.Models;
using Vaeron.Kernel;

namespace Vaeron.HelixStudio.Settings.Internal;

internal sealed class SettingsService(
    SettingsStore store,
    StartupRegistrationStore startupRegistration,
    PowerManagementStore powerManagement,
    UiDensityRuntime uiDensity)
{
    public event Action? Changed;

    /// <summary>電源 8 欄一律即時查 Windows 現況組成快照（離線真實鏡像），查詢失敗的欄位退回資料庫快取值；
    /// 其餘欄位維持原本的資料庫讀取。每次呼叫都重新查詢，不在記憶體跨次呼叫沿用上次結果。</summary>
    public async Task<AppSettingsSnapshot> LoadSettingsAsync()
    {
        var row = await store.QueryAppSettingsAsync();
        var live = await powerManagement.QueryCurrentStateAsync(row);
        return ToSnapshot(row, live);
    }

    public async Task<Result> UpdateSettingsAsync(AppSettingsSnapshot settings)
    {
        await store.InsertAppSettingsAsync(new AppSettingsRow
        {
            theme_pack = settings.ThemePackId,
            background_animation = settings.BackgroundAnimationEnabled,
            ui_scale = settings.UiScalePercent,
            data_density = settings.DataDensity.ToString(),
            sidebar_collapsed_default = settings.SidebarCollapsedByDefault,
            start_with_windows = settings.StartWithWindows,
            startup_page = settings.StartupPage,
            show_event_badge = settings.ShowEventBadge,
            power_mode = settings.PowerMode,
            ac_sleep_minutes = settings.AcSleepMinutes,
            battery_sleep_minutes = settings.BatterySleepMinutes,
            screen_timeout_minutes = settings.ScreenTimeoutMinutes,
            hibernate_enabled = settings.HibernateEnabled,
            fast_startup_enabled = settings.FastStartupEnabled,
            power_button_action = settings.PowerButtonAction,
            lid_close_action = settings.LidCloseAction,
        });

        uiDensity.ApplySetDensity(settings.DataDensity);

        var registry = startupRegistration.ApplyStartWithWindows(settings.StartWithWindows);

        Changed?.Invoke();
        return registry.IsSuccess ? Ok("已更新") : Result.Fail(registry.Kind, $"設定已儲存，但{registry.Message}");
    }

    /// <summary>更新側邊欄預設摺疊狀態</summary>
    public async Task<Result> UpdateSidebarCollapsedDefaultAsync(bool collapsed)
    {
        var current = await LoadSettingsAsync();
        return await UpdateSettingsAsync(current with { SidebarCollapsedByDefault = collapsed });
    }

    /// <summary>啟動時校正開機自動啟動的登錄機碼狀態</summary>
    public async Task ReconcileStartupRegistrationAsync()
    {
        var row = await store.QueryAppSettingsAsync();
        if (row.start_with_windows)
            startupRegistration.ApplyStartWithWindows(true);

        var actuallyRegistered = startupRegistration.QueryIsRegisteredForCurrentPath();
        if (actuallyRegistered != row.start_with_windows)
        {
            row.start_with_windows = actuallyRegistered;
            await store.InsertAppSettingsAsync(row);
        }
    }

    /// <summary>電源欄位改成先套用系統、成功才把新值寫回資料庫當快取——資料庫不再是顯示值來源，
    /// 只在下次查詢系統失敗時當離線備援，見 LoadSettingsAsync。</summary>
    public async Task<Result> ApplyPowerModeAsync(string mode)
    {
        var applied = await powerManagement.ApplyPowerModeAsync(mode);
        if (!applied.IsSuccess) return applied;
        var current = await LoadSettingsAsync();
        await UpdateSettingsAsync(current with { PowerMode = mode });
        return Ok(applied.Message == "成功" ? "已更新" : applied.Message);
    }

    public async Task<Result> ApplySleepTimeoutsAsync(SleepTimeoutsRequest request)
    {
        var applied = await powerManagement.ApplySleepTimeoutAsync(new SleepTimeoutCommand(request.AcMinutes, request.BatteryMinutes));
        if (!applied.IsSuccess) return applied;
        var current = await LoadSettingsAsync();
        await UpdateSettingsAsync(current with { AcSleepMinutes = request.AcMinutes, BatterySleepMinutes = request.BatteryMinutes });
        return Ok("已更新");
    }

    public async Task<Result> ApplyScreenTimeoutAsync(int minutes)
    {
        var applied = await powerManagement.ApplyScreenTimeoutAsync(minutes);
        if (!applied.IsSuccess) return applied;
        var current = await LoadSettingsAsync();
        await UpdateSettingsAsync(current with { ScreenTimeoutMinutes = minutes });
        return Ok("已更新");
    }

    public async Task<Result> ApplyHibernateAsync(bool enabled)
    {
        var applied = await powerManagement.ApplyHibernateAsync(enabled);
        if (!applied.IsSuccess) return applied;
        var current = await LoadSettingsAsync();
        await UpdateSettingsAsync(current with { HibernateEnabled = enabled });
        return Ok("已更新");
    }

    public async Task<Result> ApplyFastStartupAsync(bool enabled)
    {
        var applied = powerManagement.ApplyFastStartup(enabled);
        if (!applied.IsSuccess) return applied;
        var current = await LoadSettingsAsync();
        await UpdateSettingsAsync(current with { FastStartupEnabled = enabled });
        return Ok("已更新");
    }

    public async Task<Result> ApplyPowerButtonActionAsync(string action)
    {
        var applied = await powerManagement.ApplyPowerButtonActionAsync(action);
        if (!applied.IsSuccess) return applied;
        var current = await LoadSettingsAsync();
        await UpdateSettingsAsync(current with { PowerButtonAction = action });
        return Ok(applied.Message == "成功" ? "已更新" : applied.Message);
    }

    public async Task<Result> ApplyLidCloseActionAsync(string action)
    {
        var applied = await powerManagement.ApplyLidCloseActionAsync(action);
        if (!applied.IsSuccess) return applied;
        var current = await LoadSettingsAsync();
        await UpdateSettingsAsync(current with { LidCloseAction = action });
        return Ok(applied.Message == "成功" ? "已更新" : applied.Message);
    }

    /// <summary>闔螢幕顯示關閉選單（PowerMenuRuntime）：這裡不改任何持久化資料值，
    /// 只是把使用者在選單上的選擇轉成立即執行的系統動作，Service 只做轉發，不重複 Store 的判斷</summary>
    public Task<Result> ExecuteSleepNowAsync() => Task.FromResult(powerManagement.ApplySleepNow());

    public Task<Result> ExecuteHibernateNowAsync() => Task.FromResult(powerManagement.ApplyHibernateNow());

    public Task<Result> ExecuteShutdownNowAsync() => powerManagement.ApplyShutdownNowAsync();

    private static Result Ok(string message) => new() { Kind = ResultKind.Success, Message = message };

    /// <summary>電源 8 欄以 live（即時查詢）優先，查詢失敗（null）才退回資料庫快取 x。</summary>
    private static AppSettingsSnapshot ToSnapshot(AppSettingsRow x, PowerStateSnapshot live) => new()
    {
        ThemePackId = x.theme_pack,
        BackgroundAnimationEnabled = x.background_animation,
        UiScalePercent = x.ui_scale,
        DataDensity = Enum.Parse<DataDensity>(x.data_density),
        SidebarCollapsedByDefault = x.sidebar_collapsed_default,
        StartWithWindows = x.start_with_windows,
        StartupPage = x.startup_page,
        ShowEventBadge = x.show_event_badge,
        PowerMode = live.PowerMode ?? x.power_mode,
        AcSleepMinutes = live.AcSleepMinutes ?? x.ac_sleep_minutes,
        BatterySleepMinutes = live.BatterySleepMinutes ?? x.battery_sleep_minutes,
        ScreenTimeoutMinutes = live.ScreenTimeoutMinutes ?? x.screen_timeout_minutes,
        HibernateEnabled = live.HibernateEnabled ?? x.hibernate_enabled,
        FastStartupEnabled = live.FastStartupEnabled ?? x.fast_startup_enabled,
        PowerButtonAction = live.PowerButtonAction ?? x.power_button_action,
        LidCloseAction = live.LidCloseAction ?? x.lid_close_action,
    };
}
