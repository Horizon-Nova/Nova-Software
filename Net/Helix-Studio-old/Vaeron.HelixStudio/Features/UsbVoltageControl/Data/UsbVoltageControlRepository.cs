using Microsoft.EntityFrameworkCore;

namespace Vaeron.HelixStudio.Features.UsbVoltageControl.Data;

public sealed class UsbVoltageControlRepository(IDbContextFactory<UsbVoltageControlDbContext> dbFactory)
{
    #region 專用查詢方法

    public async Task<List<usb_target_voltage>> QueryTargetVoltageListAsync()
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

    #endregion

    #region 基本 CRUD 操作

    public async Task InsertTargetVoltageAsync(string deviceId, double target)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.usb_target_voltages.FirstOrDefaultAsync(x => x.device_id == deviceId);
        if (existing is null)
            db.usb_target_voltages.Add(new usb_target_voltage { device_id = deviceId, target_voltage = target });
        else
            existing.target_voltage = target;
        await db.SaveChangesAsync();
    }

    public async Task InsertGlobalAutoCorrectAsync(bool enabled)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var row = await db.usb_global_settings.FirstOrDefaultAsync(x => x.id == 1);
        if (row is null)
            db.usb_global_settings.Add(new usb_global_setting { id = 1, auto_correct_enabled = enabled });
        else
            row.auto_correct_enabled = enabled;
        await db.SaveChangesAsync();
    }

    #endregion
}
