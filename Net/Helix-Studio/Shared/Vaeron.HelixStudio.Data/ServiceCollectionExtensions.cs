using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.Data.Internal;
using Vaeron.HelixStudio.Kernel;

namespace Vaeron.HelixStudio.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHelixStudioData(this IServiceCollection services)
    {
        services.AddSingleton<DatabaseOptions>();
        services.AddSingleton<IDatabaseFacade, DatabaseFacade>();

        // 取樣迴圈與畫面各自在自己的時機建立 context，故用工廠而非共用一個實例。
        services.AddDbContextFactory<HelixStudioContext>((provider, options) =>
            options.UseSqlite($"Data Source={provider.GetRequiredService<IDatabaseFacade>().FilePath}"));

        return services;
    }
}
