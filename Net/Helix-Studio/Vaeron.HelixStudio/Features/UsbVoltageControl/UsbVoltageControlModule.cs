using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.Features.UsbVoltageControl.Data;
using Vaeron.SharedKernel;

namespace Vaeron.HelixStudio.Features.UsbVoltageControl;

public static class UsbVoltageControlModule
{
    public static IServiceCollection AddUsbVoltageControlFeature(this IServiceCollection services)
    {
        services.AddDbContextFactory<UsbVoltageControlDbContext>((sp, o) =>
            o.UseSqlite($"Data Source={sp.GetRequiredService<AppDataPath>().DatabasePath}"));
        services.AddSingleton<UsbVoltageControlRepository>();
        services.AddSingleton<UsbDeviceVoltageRepository>();
        services.AddSingleton<UsbVoltageControlDomain>();
        services.AddTransient<UsbVoltageControlViewModel>();
        services.AddTransient<UsbVoltageControlView>();
        return services;
    }
}
