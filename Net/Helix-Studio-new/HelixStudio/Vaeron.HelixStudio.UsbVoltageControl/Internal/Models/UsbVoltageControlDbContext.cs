using Microsoft.EntityFrameworkCore;

namespace Vaeron.HelixStudio.UsbVoltageControl.Internal.Models;

internal sealed class UsbVoltageControlDbContext(DbContextOptions<UsbVoltageControlDbContext> options) : DbContext(options)
{
    public DbSet<UsbTargetVoltageRow> usb_target_voltages => Set<UsbTargetVoltageRow>();
    public DbSet<UsbGlobalSettingRow> usb_global_settings => Set<UsbGlobalSettingRow>();
}

internal sealed class UsbTargetVoltageRow
{
    public int id { get; set; }
    public string device_id { get; set; } = "";
    public double target_voltage { get; set; }
}

/// <summary>單列表：全域自動修正開關</summary>
internal sealed class UsbGlobalSettingRow
{
    public int id { get; set; }
    public bool auto_correct_enabled { get; set; }
}
