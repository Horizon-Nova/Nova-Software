using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.Kernel;
using Vaeron.HelixStudio.Shell.Internal;

namespace Vaeron.HelixStudio.Shell;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddShell(this IServiceCollection services)
    {
        services.AddSingleton<ShellStore>();
        services.AddSingleton<ShellLogic>();
        services.AddSingleton<ShellService>();
        services.AddSingleton<IShellFacade, ShellFacade>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<IEventSink>(provider => provider.GetRequiredService<ShellViewModel>());
        return services;
    }
}
