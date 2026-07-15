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
            theme_mode = settings.ThemeMode.ToString(),
            accent_hue = settings.AccentHue,
            accent_chroma = settings.AccentChroma,
            ui_scale = settings.UiScalePercent,
            data_density = settings.DataDensity.ToString(),
            sidebar_collapsed_default = settings.SidebarCollapsedByDefault,
            start_with_windows = settings.StartWithWindows,
            startup_page = settings.StartupPage,
            show_event_badge = settings.ShowEventBadge,
        });

        // 資料庫存的是使用者「想要」的狀態，登錄機碼的實際狀態要跟著同步套用，不然畫面顯示
        // 已開啟但機碼其實沒寫入（或反過來），兩邊會對不上。
        var (registrySuccess, registryMessage) = startupRegistration.ApplyStartWithWindows(settings.StartWithWindows);

        Changed?.Invoke();
        return registrySuccess ? ResultContract.Ok("已更新") : ResultContract.Fail($"設定已儲存，但{registryMessage}");
    }

    /// <summary>blueprint 的「預設摺疊側邊欄」在設計稿裡其實不是獨立的「下次啟動預設值」，
    /// 是跟殼層側邊欄同一顆狀態（Settings 畫面的開關跟側邊欄漢堡選單呼叫同一個
    /// onToggleSidebar）——MainShellViewModel 每次即時切換側邊欄摺疊/展開都會呼叫這個方法
    /// 寫回資料庫，讓兩邊永遠同步，不是各管各的兩份狀態。只改這一個欄位，其餘設定值
    /// 讀現有快照原樣帶過，不會覆蓋使用者在外觀/日誌等其他分類已存的設定。</summary>
    public async Task<(bool success, string message)> UpdateSidebarCollapsedDefaultAsync(bool collapsed)
    {
        var current = await LoadSettingsAsync();
        return await UpdateSettingsAsync(current with { SidebarCollapsedByDefault = collapsed });
    }

    #endregion

    private static AppSettingsSnapshot ToSnapshot(app_settings x) => new()
    {
        ThemeMode = Enum.Parse<ThemeMode>(x.theme_mode),
        AccentHue = x.accent_hue,
        AccentChroma = x.accent_chroma,
        UiScalePercent = x.ui_scale,
        DataDensity = Enum.Parse<DataDensity>(x.data_density),
        SidebarCollapsedByDefault = x.sidebar_collapsed_default,
        StartWithWindows = x.start_with_windows,
        StartupPage = x.startup_page,
        ShowEventBadge = x.show_event_badge,
    };
}
