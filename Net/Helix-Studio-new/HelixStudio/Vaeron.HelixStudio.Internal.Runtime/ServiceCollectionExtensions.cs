using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.Internal.Runtime.AppRuntime;
using Vaeron.HelixStudio.Internal.Runtime.Themes;

namespace Vaeron.HelixStudio.Internal.Runtime;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHelixStudioRuntime(this IServiceCollection services)
    {
        services.AddSingleton<AuditLogging>();
        services.AddSingleton<IAuditSink>(sp => sp.GetRequiredService<AuditLogging>());
        services.AddSingleton<ActiveWorkspaceRuntime>();
        services.AddSingleton<FileStorage>();
        services.AddSingleton<ThemePackService>();

        services.AddSingleton<NavigationRuntime>();
        services.AddSingleton<ConfirmationRuntime>();
        services.AddSingleton<NotificationRuntime>();
        services.AddSingleton<PowerMenuRuntime>();
        services.AddSingleton<UiDensityRuntime>();

        return services;
    }
}
