using Microsoft.EntityFrameworkCore;
using Vaeron.HelixStudio.Settings.Internal.Models;

namespace Vaeron.HelixStudio.Settings.Internal;

internal sealed class SettingsStore(IDbContextFactory<SettingsDbContext> dbFactory)
{
    public async Task<AppSettingsRow> QueryAppSettingsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.app_settings_rows.FirstOrDefaultAsync(x => x.id == 1) ?? new AppSettingsRow { id = 1 };
    }

    public async Task InsertAppSettingsAsync(AppSettingsRow data)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.app_settings_rows.FirstOrDefaultAsync(x => x.id == 1);
        if (existing is null)
        {
            data.id = 1;
            db.app_settings_rows.Add(data);
        }
        else
        {
            existing.theme_pack = data.theme_pack;
            existing.background_animation = data.background_animation;
            existing.ui_scale = data.ui_scale;
            existing.data_density = data.data_density;
            existing.sidebar_collapsed_default = data.sidebar_collapsed_default;
            existing.start_with_windows = data.start_with_windows;
            existing.startup_page = data.startup_page;
            existing.show_event_badge = data.show_event_badge;
            existing.power_mode = data.power_mode;
            existing.ac_sleep_minutes = data.ac_sleep_minutes;
            existing.battery_sleep_minutes = data.battery_sleep_minutes;
            existing.screen_timeout_minutes = data.screen_timeout_minutes;
            existing.hibernate_enabled = data.hibernate_enabled;
            existing.fast_startup_enabled = data.fast_startup_enabled;
            existing.power_button_action = data.power_button_action;
            existing.lid_close_action = data.lid_close_action;
        }
        await db.SaveChangesAsync();
    }
}
