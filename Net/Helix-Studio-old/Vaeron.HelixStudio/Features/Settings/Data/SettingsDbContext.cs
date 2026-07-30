using Microsoft.EntityFrameworkCore;

namespace Vaeron.HelixStudio.Features.Settings.Data;

public sealed class SettingsDbContext(DbContextOptions<SettingsDbContext> options) : DbContext(options)
{
    public DbSet<app_settings> app_settings_rows => Set<app_settings>();
}

/// <summary>單列表，id 永遠是 1，見 configuration.md 的 SQLite 判準。</summary>
public sealed class app_settings
{
    public int id { get; set; }
    public string theme_pack { get; set; } = "default";
    public bool background_animation { get; set; } = true;
    public int ui_scale { get; set; } = 100;
    public string data_density { get; set; } = "Comfortable";
    public bool sidebar_collapsed_default { get; set; }
    public bool start_with_windows { get; set; }
    public string startup_page { get; set; } = "apps";
    public bool show_event_badge { get; set; } = true;
    public string power_mode { get; set; } = "平衡";
    public int ac_sleep_minutes { get; set; } = 30;
    public int battery_sleep_minutes { get; set; } = 15;
    public int screen_timeout_minutes { get; set; } = 10;
    public bool hibernate_enabled { get; set; } = true;
    public bool fast_startup_enabled { get; set; } = true;
    public string power_button_action { get; set; } = "睡眠";
    public string lid_close_action { get; set; } = "睡眠";
}
