using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.Features.WorkspaceManagement.Data;
using Vaeron.SharedKernel;

namespace Vaeron.HelixStudio.Features.WorkspaceManagement;

public static class WorkspaceManagementModule
{
    public static IServiceCollection AddWorkspaceManagementFeature(this IServiceCollection services)
    {
        services.AddDbContextFactory<WorkspaceManagementDbContext>((sp, o) =>
            o.UseSqlite($"Data Source={sp.GetRequiredService<AppDataPath>().DatabasePath}"));
        services.AddSingleton<WorkspaceManagementRepository>();
        services.AddSingleton<WorkspaceManagementDomain>();
        services.AddSingleton<WorkspaceManagementFacade>();
        return services;
    }
}
