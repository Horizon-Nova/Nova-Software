using Microsoft.EntityFrameworkCore;
using Vaeron.HelixStudio.UsbVoltageControl.Internal.Models;

namespace Vaeron.HelixStudio.UsbVoltageControl.Internal;

internal sealed record SetTargetVoltageCommand(string DeviceId, double Target);

internal sealed class UsbVoltageControlStore(IDbContextFactory<UsbVoltageControlDbContext> dbFactory)
{
    public async Task<List<UsbTargetVoltageRow>> QueryTargetVoltageListAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.usb_target_voltages.ToListAsync();
    }

    public async Task<bool> QueryGlobalAutoCorrectAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var row = await db.usb_global_settings.FirstOrDefaultAsync(x => x.id == 1);
        return row?.auto_correct_enabled ?? false;
    }

    public async Task InsertTargetVoltageAsync(SetTargetVoltageCommand command)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.usb_target_voltages.FirstOrDefaultAsync(x => x.device_id == command.DeviceId);
        if (existing is null)
            db.usb_target_voltages.Add(new UsbTargetVoltageRow { device_id = command.DeviceId, target_voltage = command.Target });
        else
            existing.target_voltage = command.Target;
        await db.SaveChangesAsync();
    }

    public async Task InsertGlobalAutoCorrectAsync(bool enabled)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var row = await db.usb_global_settings.FirstOrDefaultAsync(x => x.id == 1);
        if (row is null)
            db.usb_global_settings.Add(new UsbGlobalSettingRow { id = 1, auto_correct_enabled = enabled });
        else
            row.auto_correct_enabled = enabled;
        await db.SaveChangesAsync();
    }
}
