using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.Features.AppLibrary.Data;
using Vaeron.HelixStudio.Features.AppsManagement;
using Vaeron.SharedKernel;

namespace Vaeron.HelixStudio.Features.AppLibrary;

public static class AppLibraryModule
{
    public static IServiceCollection AddAppLibraryFeature(this IServiceCollection services)
    {
        services.AddDbContextFactory<AppLibraryDbContext>((sp, o) =>
            o.UseSqlite($"Data Source={sp.GetRequiredService<AppDataPath>().DatabasePath}"));
        services.AddSingleton<AppLibraryRepository>();
        services.AddSingleton(sp =>
            new BuiltInCatalogRepository(Path.Combine(AppContext.BaseDirectory, "Catalog")));
        services.AddSingleton(sp => new AppLibraryDomain(
            sp.GetRequiredService<AppLibraryRepository>(),
            sp.GetRequiredService<BuiltInCatalogRepository>(),
            sp.GetRequiredService<AppsManagementFacade>(),
            sp.GetRequiredService<FileStorage>(),
            Path.Combine(sp.GetRequiredService<AppDataPath>().LocalRoot, "ImportedTools")));
        services.AddTransient<AppLibraryViewModel>();
        services.AddTransient<AppLibraryView>();
        return services;
    }
}
