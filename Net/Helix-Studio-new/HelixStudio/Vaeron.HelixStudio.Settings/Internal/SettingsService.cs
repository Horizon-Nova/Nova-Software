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

    public async Task<AppSettingsSnapshot> LoadSettingsAsync()
    {
        var row = await store.QueryAppSettingsAsync();
        return ToSnapshot(row);
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

    public async Task<Result> ApplyPowerModeAsync(string mode)
    {
        var current = await LoadSettingsAsync();
        await UpdateSettingsAsync(current with { PowerMode = mode });
        var applied = await powerManagement.ApplyPowerModeAsync(mode);
        return applied.IsSuccess ? Ok(applied.Message == "成功" ? "已更新" : applied.Message) : applied;
    }

    public async Task<Result> ApplySleepTimeoutsAsync(SleepTimeoutsRequest request)
    {
        var current = await LoadSettingsAsync();
        await UpdateSettingsAsync(current with { AcSleepMinutes = request.AcMinutes, BatterySleepMinutes = request.BatteryMinutes });
        var applied = await powerManagement.ApplySleepTimeoutAsync(new SleepTimeoutCommand(request.AcMinutes, request.BatteryMinutes));
        return applied.IsSuccess ? Ok("已更新") : applied;
    }

    public async Task<Result> ApplyScreenTimeoutAsync(int minutes)
    {
        var current = await LoadSettingsAsync();
        await UpdateSettingsAsync(current with { ScreenTimeoutMinutes = minutes });
        var applied = await powerManagement.ApplyScreenTimeoutAsync(minutes);
        return applied.IsSuccess ? Ok("已更新") : applied;
    }

    public async Task<Result> ApplyHibernateAsync(bool enabled)
    {
        var current = await LoadSettingsAsync();
        await UpdateSettingsAsync(current with { HibernateEnabled = enabled });
        var applied = await powerManagement.ApplyHibernateAsync(enabled);
        return applied.IsSuccess ? Ok("已更新") : applied;
    }

    public async Task<Result> ApplyFastStartupAsync(bool enabled)
    {
        var current = await LoadSettingsAsync();
        await UpdateSettingsAsync(current with { FastStartupEnabled = enabled });
        var applied = powerManagement.ApplyFastStartup(enabled);
        return applied.IsSuccess ? Ok("已更新") : applied;
    }

    public async Task<Result> ApplyPowerButtonActionAsync(string action)
    {
        var current = await LoadSettingsAsync();
        await UpdateSettingsAsync(current with { PowerButtonAction = action });
        var applied = await powerManagement.ApplyPowerButtonActionAsync(action);
        return applied.IsSuccess ? Ok(applied.Message == "成功" ? "已更新" : applied.Message) : applied;
    }

    public async Task<Result> ApplyLidCloseActionAsync(string action)
    {
        var current = await LoadSettingsAsync();
        await UpdateSettingsAsync(current with { LidCloseAction = action });
        var applied = await powerManagement.ApplyLidCloseActionAsync(action);
        return applied.IsSuccess ? Ok(applied.Message == "成功" ? "已更新" : applied.Message) : applied;
    }

    /// <summary>啟動時把已持久化的電源設定單向重套到系統，best-effort，不回報結果、不做雙向校正</summary>
    public async Task ReconcilePowerSettingsAsync()
    {
        var settings = await LoadSettingsAsync();
        await powerManagement.ApplyPowerModeAsync(settings.PowerMode);
        await powerManagement.ApplySleepTimeoutAsync(new SleepTimeoutCommand(settings.AcSleepMinutes, settings.BatterySleepMinutes));
        await powerManagement.ApplyScreenTimeoutAsync(settings.ScreenTimeoutMinutes);
        await powerManagement.ApplyHibernateAsync(settings.HibernateEnabled);
        powerManagement.ApplyFastStartup(settings.FastStartupEnabled);
        await powerManagement.ApplyPowerButtonActionAsync(settings.PowerButtonAction);
        await powerManagement.ApplyLidCloseActionAsync(settings.LidCloseAction);
    }

    /// <summary>闔螢幕顯示關閉選單（PowerMenuRuntime）：這裡不改任何持久化資料值，
    /// 只是把使用者在選單上的選擇轉成立即執行的系統動作，Service 只做轉發，不重複 Store 的判斷</summary>
    public Task<Result> ExecuteSleepNowAsync() => Task.FromResult(powerManagement.ApplySleepNow());

    public Task<Result> ExecuteHibernateNowAsync() => Task.FromResult(powerManagement.ApplyHibernateNow());

    public Task<Result> ExecuteShutdownNowAsync() => powerManagement.ApplyShutdownNowAsync();

    private static Result Ok(string message) => new() { Kind = ResultKind.Success, Message = message };

    private static AppSettingsSnapshot ToSnapshot(AppSettingsRow x) => new()
    {
        ThemePackId = x.theme_pack,
        BackgroundAnimationEnabled = x.background_animation,
        UiScalePercent = x.ui_scale,
        DataDensity = Enum.Parse<DataDensity>(x.data_density),
        SidebarCollapsedByDefault = x.sidebar_collapsed_default,
        StartWithWindows = x.start_with_windows,
        StartupPage = x.startup_page,
        ShowEventBadge = x.show_event_badge,
        PowerMode = x.power_mode,
        AcSleepMinutes = x.ac_sleep_minutes,
        BatterySleepMinutes = x.battery_sleep_minutes,
        ScreenTimeoutMinutes = x.screen_timeout_minutes,
        HibernateEnabled = x.hibernate_enabled,
        FastStartupEnabled = x.fast_startup_enabled,
        PowerButtonAction = x.power_button_action,
        LidCloseAction = x.lid_close_action,
    };
}
