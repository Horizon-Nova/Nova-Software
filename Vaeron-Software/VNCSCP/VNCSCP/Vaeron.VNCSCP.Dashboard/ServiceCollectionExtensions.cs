using Microsoft.Extensions.DependencyInjection;

namespace Vaeron.VNCSCP.Dashboard;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDashboard(this IServiceCollection services)
    {
        services.AddSingleton<DashboardService>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<DashboardView>();
        return services;
    }
}
