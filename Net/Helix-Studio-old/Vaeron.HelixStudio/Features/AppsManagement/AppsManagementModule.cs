using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.Features.AppsManagement.Data;
using Vaeron.SharedKernel;

namespace Vaeron.HelixStudio.Features.AppsManagement;

public static class AppsManagementModule
{
    public static IServiceCollection AddAppsManagementFeature(this IServiceCollection services)
    {
        services.AddDbContextFactory<AppsManagementDbContext>((sp, o) =>
            o.UseSqlite($"Data Source={sp.GetRequiredService<AppDataPath>().DatabasePath}"));
        services.AddSingleton<InstalledToolsRepository>();
        services.AddSingleton<ToolProcessRepository>();
        services.AddSingleton<AppsManagementDomain>();
        services.AddSingleton<AppsManagementFacade>();
        services.AddTransient<AppsManagementViewModel>();
        services.AddTransient<AppsManagementView>();
        return services;
    }
}
