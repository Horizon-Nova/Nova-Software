using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.Internal.Runtime;
using Vaeron.HelixStudio.Settings.Internal;
using Vaeron.HelixStudio.Settings.Internal.Models;
using Vaeron.Kernel;

namespace Vaeron.HelixStudio.Settings;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSettingsFeature(this IServiceCollection services)
    {
        services.AddDbContextFactory<SettingsDbContext>((_, o) =>
            o.UseSqlite($"Data Source={Path.Combine(Paths.Local, "app.db")}"));

        services.AddSingleton<SettingsStore>();
        services.AddSingleton<StartupRegistrationStore>();
        services.AddSingleton<PowerManagementStore>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<SettingsFacade>();
        services.AddSingleton<ISettingsFacade>(sp =>
            FacadeAuditProxy.Wrap<ISettingsFacade>(
                sp.GetRequiredService<SettingsFacade>(), sp.GetServices<IAuditSink>()));
        services.AddSingleton<IFeatureDatabaseInitializer, SettingsDatabaseInitializer>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SettingsView>();
        return services;
    }
}
