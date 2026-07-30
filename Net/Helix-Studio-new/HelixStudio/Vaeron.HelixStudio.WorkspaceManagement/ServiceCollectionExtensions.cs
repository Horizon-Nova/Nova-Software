using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.Internal.Runtime;
using Vaeron.HelixStudio.WorkspaceManagement.Internal;
using Vaeron.HelixStudio.WorkspaceManagement.Internal.Models;
using Vaeron.Kernel;

namespace Vaeron.HelixStudio.WorkspaceManagement;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorkspaceManagementFeature(this IServiceCollection services)
    {
        services.AddDbContextFactory<WorkspaceManagementDbContext>((_, o) =>
            o.UseSqlite($"Data Source={Path.Combine(Paths.Local, "app.db")}"));

        services.AddSingleton<WorkspaceManagementStore>();
        services.AddSingleton<WorkspaceManagementService>();
        services.AddSingleton<WorkspaceManagementFacade>();
        services.AddSingleton<IWorkspaceManagementFacade>(sp =>
            FacadeAuditProxy.Wrap<IWorkspaceManagementFacade>(
                sp.GetRequiredService<WorkspaceManagementFacade>(), sp.GetServices<IAuditSink>()));
        services.AddSingleton<IFeatureDatabaseInitializer, WorkspaceManagementDatabaseInitializer>();
        return services;
    }
}
