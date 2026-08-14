using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.Data;
using Vaeron.HelixStudio.Sampling.Internal;
using Vaeron.HelixStudio.Sampling.Model;

namespace Vaeron.HelixStudio.Sampling;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSampling(this IServiceCollection services)
    {
        services.AddSingleton<IModelParts, SamplingModelParts>();
        services.AddSingleton<SamplingStore>();
        services.AddSingleton<ProcessProbe>();
        services.AddSingleton<PerfCounterProvider>();
        services.AddSingleton<ISamplerFacade, SamplingLoop>();
        return services;
    }
}
