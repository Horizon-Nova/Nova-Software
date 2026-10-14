using Microsoft.Extensions.DependencyInjection;
using Vaeron.VNCSCP.ConnectionManagement.Internal;
using Vaeron.VNCSCP.Internal.Contracts;

namespace Vaeron.VNCSCP.ConnectionManagement;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConnectionManagement(this IServiceCollection services)
    {
        services.AddSingleton<ConnectionManagementStore>();
        services.AddSingleton<RfbAuthClient>();
        services.AddSingleton<ConnectionManagementLogic>();
        services.AddSingleton<ConnectionManagementService>();
        services.AddSingleton<IDeployTargetProvider>(provider => provider.GetRequiredService<ConnectionManagementService>());
        services.AddSingleton<IConnectionManagementFacade, ConnectionManagementFacade>();
        return services;
    }
}
