using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vaeron.Kernel;

namespace Vaeron._App_.App;

/// <summary>
/// 宿主設定。下列每一項都必須明確設定,不依賴預設值：
/// 註冊驗證於直接建立宿主時不會自動套用;
/// 背景服務失敗行為的預設值來自伺服器情境(外部編排器會重啟);
/// 關閉逾時預設值偏短。
/// </summary>
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
            .ConfigureServices((_, services) =>
            {
                services.Configure<HostOptions>(options =>
                {
                    // 無外部編排器時必須覆寫,否則單一背景服務失敗會停掉整個進程
                    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
                    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
                });

                Composition.Register(services);
            });
    }
}
