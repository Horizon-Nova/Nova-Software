using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.AppLibrary.Internal;
using Vaeron.HelixStudio.AppLibrary.Internal.Models;
using Vaeron.HelixStudio.AppsManagement;
using Vaeron.HelixStudio.Internal.Runtime;
using Vaeron.Kernel;

namespace Vaeron.HelixStudio.AppLibrary;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppLibraryFeature(this IServiceCollection services)
    {
        services.AddDbContextFactory<AppLibraryDbContext>((_, o) =>
            o.UseSqlite($"Data Source={Path.Combine(Paths.Local, "app.db")}"));

        services.AddSingleton<AppLibraryStore>();
        services.AddSingleton(_ => new BuiltInCatalogStore(Path.Combine(Paths.InstallDirectory, "Catalog")));
        services.AddSingleton(sp => new AppLibraryService(
            sp.GetRequiredService<AppLibraryStore>(),
            sp.GetRequiredService<BuiltInCatalogStore>(),
            sp.GetRequiredService<IAppsManagementFacade>(),
            sp.GetRequiredService<FileStorage>(),
            Path.Combine(Paths.Local, "ImportedTools")));
        services.AddSingleton<AppLibraryFacade>();
        services.AddSingleton<IAppLibraryFacade>(sp =>
            FacadeAuditProxy.Wrap<IAppLibraryFacade>(
                sp.GetRequiredService<AppLibraryFacade>(), sp.GetServices<IAuditSink>()));
        services.AddSingleton<IFeatureDatabaseInitializer, AppLibraryDatabaseInitializer>();
        services.AddTransient<AppLibraryViewModel>();
        services.AddTransient<AppLibraryView>();
        return services;
    }
}
