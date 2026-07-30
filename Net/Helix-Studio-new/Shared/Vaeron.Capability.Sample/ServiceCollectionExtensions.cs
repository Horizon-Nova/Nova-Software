using Microsoft.Extensions.DependencyInjection;
using Vaeron.Capability.Sample.Internal;

namespace Vaeron.Capability.Sample;

/// <summary>組件描述自身所需,由根組件決定是否呼叫。組件不知道自己被誰組裝。</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSampleCapability(this IServiceCollection services)
    {
        services.AddOptions<SampleOptions>()
            .BindConfiguration(SampleOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ISampleService, SampleService>();
        return services;
    }
}
