using Microsoft.Extensions.DependencyInjection;
using Vaeron.Platform.AppRuntime;
using Vaeron.SharedKernel;

namespace Vaeron.Platform;

public static class PlatformModule
{
    public static IServiceCollection AddPlatform(this IServiceCollection services, AppDataPath paths)
    {
        services.AddSingleton(paths);
        services.AddSingleton<DpapiProtector>();
        services.AddSingleton<FileStorage>();
        services.AddSingleton(new AuditLogging(paths.LogDirectory));

        services.AddSingleton<NavigationRuntime>();
        services.AddSingleton<ConfirmationRuntime>();
        services.AddSingleton<NotificationRuntime>();

        return services;
    }
}
