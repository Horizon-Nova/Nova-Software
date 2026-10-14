using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vaeron.Kernel;

namespace Vaeron.VNCSCP.App;

/// <summary>宿主設定。</summary>
/// <remarks>
/// Host 只提供組態、相依注入與日誌，不接管應用程式生命週期——WPF 有自己的訊息迴圈,
/// 與 host.RunAsync() 的阻塞式生命週期衝突。主迴圈由 Application.Run() 驅動。
/// </remarks>
public static class HostSetup
{
    public static IHostBuilder Create(string[] args, string appName)
    {
        Paths.Initialize(appName);

        return Host.CreateDefaultBuilder(args)
            .UseDefaultServiceProvider((_, options) =>
            {
                options.ValidateOnBuild = true;
                options.ValidateScopes = true;
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<HostOptions>(options =>
                {
                    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
                    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
                });

                Composition.Register(services, context.Configuration);
            });
    }
}

