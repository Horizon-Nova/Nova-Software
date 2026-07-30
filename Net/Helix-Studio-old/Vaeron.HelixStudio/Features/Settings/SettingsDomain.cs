using Vaeron.HelixStudio.Features.Settings.Contracts;
using Vaeron.HelixStudio.Features.Settings.Data;
using Vaeron.SharedKernel;

namespace Vaeron.HelixStudio.Features.Settings;

public sealed class SettingsDomain(
    SettingsRepository repo,
    StartupRegistrationRepository startupRegistration,
    PowerManagementRepository powerManagement)
{
    #region 事件中繼

    public event Action? Changed;

    #endregion

    #region 統一的查詢方法

    public async Task<AppSettingsSnapshot> LoadSettingsAsync()
    {
        var row = await repo.QueryAppSettingsAsync();
        return ToSnapshot(row);
    }

    #endregion

    #region 基本 CRUD 操作

    public async Task<(bool success, string message)> UpdateSettingsAsync(AppSettingsSnapshot settings)
    {
        await repo.InsertAppSettingsAsync(new app_settings
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

        var (registrySuccess, registryMessage) = startupRegistration.ApplyStartWithWindows(settings.StartWithWindows);

        Changed?.Invoke();
        return registrySuccess ? ResultContract.Ok("已更新") : ResultContract.Fail($"設定已儲存，但{registryMessage}");
    }

    /// <summary>更新側邊欄預設摺疊狀態</summary>
    public async Task<(bool success, string message)> UpdateSidebarCollapsedDefaultAsync(bool collapsed)
    {
        var current = await LoadSettingsAsync();
        return await UpdateSettingsAsync(current with { SidebarCollapsedByDefault = collapsed });
    }

    /// <summary>啟動時校正開機自動啟動的登錄機碼狀態</summary>
    public async Task ReconcileStartupRegistrationAsync()
    {
        var row = await repo.QueryAppSettingsAsync();
        if (row.start_with_windows)
            startupRegistration.ApplyStartWithWindows(true);

        var actuallyRegistered = startupRegistration.QueryIsRegisteredForCurrentPath();
        if (actuallyRegistered != row.start_with_windows)
        {
            row.start_with_windows = actuallyRegistered;
            await repo.InsertAppSettingsAsync(row);
        }
    }

    #endregion

    #region 電源

    public async Task<(bool success, string message)> ApplyPowerModeAsync(string mode)
    {
        var current = await LoadSettingsAsync();
        await UpdateSettingsAsync(current with { PowerMode = mode });
        var (success, message) = await powerManagement.ApplyPowerModeAsync(mode);
        return success ? ResultContract.Ok(message == "成功" ? "已更新" : message) : ResultContract.Fail(message);
    }

    public async Task<(bool success, string message)> ApplySleepTimeoutsAsync(int acMinutes, int batteryMinutes)
    {
        var current = await LoadSettingsAsync();
        await UpdateSettingsAsync(current with { AcSleepMinutes = acMinutes, BatterySleepMinutes = batteryMinutes });
        var (success, message) = await powerManagement.ApplySleepTimeoutAsync(acMinutes, batteryMinutes);
        return success ? ResultContract.Ok("已更新") : ResultContract.Fail(message);
    }

    public async Task<(bool success, string message)> ApplyScreenTimeoutAsync(int minutes)
    {
        var current = await LoadSettingsAsync();
        await UpdateSettingsAsync(current with { ScreenTimeoutMinutes = minutes });
        var (success, message) = await powerManagement.ApplyScreenTimeoutAsync(minutes);
        return success ? ResultContract.Ok("已更新") : ResultContract.Fail(message);
    }

    public async Task<(bool success, string message)> ApplyHibernateAsync(bool enabled)
    {
        var current = await LoadSettingsAsync();
        await UpdateSettingsAsync(current with { HibernateEnabled = enabled });
        var (success, message) = await powerManagement.ApplyHibernateAsync(enabled);
        return success ? ResultContract.Ok("已更新") : ResultContract.Fail(message);
    }

    public async Task<(bool success, string message)> ApplyFastStartupAsync(bool enabled)
    {
        var current = await LoadSettingsAsync();
        await UpdateSettingsAsync(current with { FastStartupEnabled = enabled });
        var (success, message) = powerManagement.ApplyFastStartup(enabled);
        return success ? ResultContract.Ok("已更新") : ResultContract.Fail(message);
    }

    public async Task<(bool success, string message)> ApplyPowerButtonActionAsync(string action)
    {
        var current = await LoadSettingsAsync();
        await UpdateSettingsAsync(current with { PowerButtonAction = action });
        var (success, message) = await powerManagement.ApplyPowerButtonActionAsync(action);
        return success ? ResultContract.Ok(message == "成功" ? "已更新" : message) : ResultContract.Fail(message);
    }

    public async Task<(bool success, string message)> ApplyLidCloseActionAsync(string action)
    {
        var current = await LoadSettingsAsync();
        await UpdateSettingsAsync(current with { LidCloseAction = action });
        var (success, message) = await powerManagement.ApplyLidCloseActionAsync(action);
        return success ? ResultContract.Ok(message == "成功" ? "已更新" : message) : ResultContract.Fail(message);
    }

    /// <summary>啟動時把已持久化的電源設定單向重套到系統，best-effort，不回報結果、不做雙向校正（見藍圖_設定.md「不做什麼」）</summary>
    public async Task ReconcilePowerSettingsAsync()
    {
        var settings = await LoadSettingsAsync();
        await powerManagement.ApplyPowerModeAsync(settings.PowerMode);
        await powerManagement.ApplySleepTimeoutAsync(settings.AcSleepMinutes, settings.BatterySleepMinutes);
        await powerManagement.ApplyScreenTimeoutAsync(settings.ScreenTimeoutMinutes);
        await powerManagement.ApplyHibernateAsync(settings.HibernateEnabled);
        powerManagement.ApplyFastStartup(settings.FastStartupEnabled);
        await powerManagement.ApplyPowerButtonActionAsync(settings.PowerButtonAction);
        await powerManagement.ApplyLidCloseActionAsync(settings.LidCloseAction);
    }

    /// <summary>闔螢幕顯示關閉選單（PowerMenuRuntime，見 app-runtime.md 邊界判準：這裡不改任何持久化資料值，
    /// 只是把使用者在選單上的選擇轉成立即執行的系統動作，Domain 只做轉發，不重複 Repository 的判斷）</summary>
    public Task<(bool success, string message)> ExecuteSleepNowAsync() => Task.FromResult(powerManagement.ApplySleepNow());

    public Task<(bool success, string message)> ExecuteHibernateNowAsync() => Task.FromResult(powerManagement.ApplyHibernateNow());

    public Task<(bool success, string message)> ExecuteShutdownNowAsync() => powerManagement.ApplyShutdownNowAsync();

    #endregion

    private static AppSettingsSnapshot ToSnapshot(app_settings x) => new()
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
