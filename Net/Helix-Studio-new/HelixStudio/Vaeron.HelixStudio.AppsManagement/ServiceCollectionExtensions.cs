using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.AppsManagement.Internal;
using Vaeron.HelixStudio.AppsManagement.Internal.Models;
using Vaeron.HelixStudio.Internal.Runtime;
using Vaeron.Kernel;

namespace Vaeron.HelixStudio.AppsManagement;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppsManagementFeature(this IServiceCollection services)
    {
        services.AddDbContextFactory<AppsManagementDbContext>((_, o) =>
            o.UseSqlite($"Data Source={Path.Combine(Paths.Local, "app.db")}"));

        services.AddSingleton<InstalledToolsStore>();
        services.AddSingleton<ToolProcessStore>();
        services.AddSingleton<AppsManagementService>();
        services.AddSingleton<AppsManagementFacade>();
        services.AddSingleton<IAppsManagementFacade>(sp =>
            FacadeAuditProxy.Wrap<IAppsManagementFacade>(
                sp.GetRequiredService<AppsManagementFacade>(), sp.GetServices<IAuditSink>()));
        services.AddSingleton<IFeatureDatabaseInitializer, AppsManagementDatabaseInitializer>();
        services.AddTransient<AppsManagementViewModel>();
        services.AddTransient<AppsManagementView>();
        return services;
    }
}
