using Microsoft.Extensions.DependencyInjection;

namespace Vaeron.HelixStudio;

/// <summary>殼層 DI 組裝入口</summary>
public static class ShellModule
{
    public static IServiceCollection AddShell(this IServiceCollection services)
    {
        services.AddTransient<MainShellViewModel>();
        services.AddTransient<MainWindow>();
        return services;
    }
}
