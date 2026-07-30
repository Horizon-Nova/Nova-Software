using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.Internal.Runtime;
using Vaeron.HelixStudio.UsbVoltageControl.Internal;
using Vaeron.HelixStudio.UsbVoltageControl.Internal.Models;
using Vaeron.Kernel;

namespace Vaeron.HelixStudio.UsbVoltageControl;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUsbVoltageControlFeature(this IServiceCollection services)
    {
        services.AddDbContextFactory<UsbVoltageControlDbContext>((_, o) =>
            o.UseSqlite($"Data Source={Path.Combine(Paths.Local, "app.db")}"));

        services.AddSingleton<UsbVoltageControlStore>();
        services.AddSingleton<UsbDeviceVoltageStore>();
        services.AddSingleton<UsbVoltageControlService>();
        services.AddSingleton<UsbVoltageControlFacade>();
        services.AddSingleton<IUsbVoltageControlFacade>(sp =>
            FacadeAuditProxy.Wrap<IUsbVoltageControlFacade>(
                sp.GetRequiredService<UsbVoltageControlFacade>(), sp.GetServices<IAuditSink>()));
        services.AddSingleton<IFeatureDatabaseInitializer, UsbVoltageControlDatabaseInitializer>();
        services.AddTransient<UsbVoltageControlViewModel>();
        services.AddTransient<UsbVoltageControlView>();
        return services;
    }
}
