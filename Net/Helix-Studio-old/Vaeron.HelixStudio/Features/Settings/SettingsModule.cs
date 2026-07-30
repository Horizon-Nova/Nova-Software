using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.Features.Settings.Data;
using Vaeron.SharedKernel;

namespace Vaeron.HelixStudio.Features.Settings;

public static class SettingsModule
{
    public static IServiceCollection AddSettingsFeature(this IServiceCollection services)
    {
        services.AddDbContextFactory<SettingsDbContext>((sp, o) =>
            o.UseSqlite($"Data Source={sp.GetRequiredService<AppDataPath>().DatabasePath}"));
        services.AddSingleton<Themes.ThemePackService>();
        services.AddSingleton<SettingsRepository>();
        services.AddSingleton<StartupRegistrationRepository>();
        services.AddSingleton<PowerManagementRepository>();
        services.AddSingleton<SettingsDomain>();
        services.AddSingleton<SettingsFacade>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SettingsView>();
        return services;
    }
}
