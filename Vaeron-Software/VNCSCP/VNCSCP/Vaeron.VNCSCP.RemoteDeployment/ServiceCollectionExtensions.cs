using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vaeron.VNCSCP.Internal.Contracts;
using Vaeron.VNCSCP.RemoteDeployment.Internal;

namespace Vaeron.VNCSCP.RemoteDeployment;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRemoteDeployment(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RemoteDeploymentOptions>()
            .Bind(configuration.GetSection(RemoteDeploymentOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<RemoteDeploymentStore>();
        services.AddSingleton<DeployPreferencesStore>();
        services.AddSingleton<RfbFileTransferClient>();
        services.AddSingleton<RemoteDeploymentLogic>();
        services.AddSingleton<RemoteDeploymentService>();
        services.AddSingleton<RemoteDeploymentRunner>();
        services.AddSingleton<IRemoteDeploymentFacade, RemoteDeploymentFacade>();
        return services;
    }
}
