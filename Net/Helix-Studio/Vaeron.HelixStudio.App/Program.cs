using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.Data;
using Vaeron.HelixStudio.Kernel;
using Vaeron.HelixStudio.Sampling;

namespace Vaeron.HelixStudio.App;

internal static class Program
{
    private const string AppName = "HelixStudio";

    [STAThread]
    private static int Main()
    {
        Paths.Initialize(AppName);

        var services = new ServiceCollection();
        Composition.Register(services);

        var options = new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true };
        using var provider = services.BuildServiceProvider(options);

        provider.GetRequiredService<IDatabaseFacade>().ApplySchema();
        provider.GetRequiredService<ISamplerFacade>().Start();

        return new App(provider).Run();
    }
}
