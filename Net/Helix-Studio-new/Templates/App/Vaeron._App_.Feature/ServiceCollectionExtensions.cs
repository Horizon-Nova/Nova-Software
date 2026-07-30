using Microsoft.Extensions.DependencyInjection;
using Vaeron._App_.Feature.Internal;

namespace Vaeron._App_.Feature;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFeature(this IServiceCollection services)
    {
        services.AddOptions<FeatureOptions>()
            .BindConfiguration(FeatureOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<FeatureStore>();
        services.AddSingleton<FeatureService>();
        services.AddSingleton<IFeatureFacade, FeatureFacade>();
        return services;
    }
}
