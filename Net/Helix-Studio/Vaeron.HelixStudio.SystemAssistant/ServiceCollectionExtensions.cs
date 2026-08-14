using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.Data;
using Vaeron.HelixStudio.SystemAssistant.Internal;
using Vaeron.HelixStudio.SystemAssistant.Model;

namespace Vaeron.HelixStudio.SystemAssistant;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSystemAssistant(this IServiceCollection services)
    {
        services.AddSingleton<IModelParts, SystemAssistantModelParts>();
        services.AddSingleton<SystemAssistantStore>();
        services.AddSingleton<ProcessControl>();
        services.AddSingleton<SystemAssistantLogic>();
        services.AddSingleton<ISystemAssistantFacade, SystemAssistantFacade>();
        services.AddSingleton<SystemAssistantViewModel>();
        return services;
    }
}
