using Microsoft.EntityFrameworkCore;

namespace Vaeron.HelixStudio.Features.UsbVoltageControl.Data;

public sealed class UsbVoltageControlDbContext(DbContextOptions<UsbVoltageControlDbContext> options) : DbContext(options)
{
    public DbSet<usb_target_voltage> usb_target_voltages => Set<usb_target_voltage>();
    public DbSet<usb_global_setting> usb_global_settings => Set<usb_global_setting>();
}

public sealed class usb_target_voltage
{
    public int id { get; set; }
    public string device_id { get; set; } = "";
    public double target_voltage { get; set; }
}

/// <summary>單列表：全域自動修正開關</summary>
public sealed class usb_global_setting
{
    public int id { get; set; }
    public bool auto_correct_enabled { get; set; }
}
