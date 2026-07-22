using Vaeron.HelixStudio.Features.Settings.Contracts;
using Vaeron.HelixStudio.Features.Settings.Data;
using Vaeron.SharedKernel;

namespace Vaeron.HelixStudio.Features.Settings;

public sealed class SettingsDomain(SettingsRepository repo, StartupRegistrationRepository startupRegistration)
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
    };
}
