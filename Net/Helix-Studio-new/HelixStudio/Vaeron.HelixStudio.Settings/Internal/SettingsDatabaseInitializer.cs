using Microsoft.EntityFrameworkCore;
using Vaeron.HelixStudio.Internal.Runtime;
using Vaeron.HelixStudio.Internal.Runtime.Themes;
using Vaeron.HelixStudio.Settings.Internal.Models;

namespace Vaeron.HelixStudio.Settings.Internal;

internal sealed class SettingsDatabaseInitializer(IDbContextFactory<SettingsDbContext> dbFactory) : IFeatureDatabaseInitializer
{
    public async Task InitializeAsync()
    {
        await DatabaseBootstrap.EnsureCreatedAsync(dbFactory);
        await DatabaseBootstrap.EnsureColumnExistsAsync(dbFactory, new ColumnMigration("app_settings_rows", "theme_pack", "TEXT", ThemePack.DefaultId));
        await DatabaseBootstrap.EnsureColumnExistsAsync(dbFactory, new ColumnMigration("app_settings_rows", "background_animation", "INTEGER", "1"));
        await DatabaseBootstrap.EnsureColumnExistsAsync(dbFactory, new ColumnMigration("app_settings_rows", "power_mode", "TEXT", "平衡"));
        await DatabaseBootstrap.EnsureColumnExistsAsync(dbFactory, new ColumnMigration("app_settings_rows", "ac_sleep_minutes", "INTEGER", "30"));
        await DatabaseBootstrap.EnsureColumnExistsAsync(dbFactory, new ColumnMigration("app_settings_rows", "battery_sleep_minutes", "INTEGER", "15"));
        await DatabaseBootstrap.EnsureColumnExistsAsync(dbFactory, new ColumnMigration("app_settings_rows", "screen_timeout_minutes", "INTEGER", "10"));
        await DatabaseBootstrap.EnsureColumnExistsAsync(dbFactory, new ColumnMigration("app_settings_rows", "hibernate_enabled", "INTEGER", "1"));
        await DatabaseBootstrap.EnsureColumnExistsAsync(dbFactory, new ColumnMigration("app_settings_rows", "fast_startup_enabled", "INTEGER", "1"));
        await DatabaseBootstrap.EnsureColumnExistsAsync(dbFactory, new ColumnMigration("app_settings_rows", "power_button_action", "TEXT", "睡眠"));
        await DatabaseBootstrap.EnsureColumnExistsAsync(dbFactory, new ColumnMigration("app_settings_rows", "lid_close_action", "TEXT", "睡眠"));
    }
}
