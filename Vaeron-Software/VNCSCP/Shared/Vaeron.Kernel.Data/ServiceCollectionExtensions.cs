using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Vaeron.Kernel.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVncscpData(this IServiceCollection services)
    {
        services.AddDbContextFactory<VncscpDbContext>(options =>
            options.UseSqlite($"Data Source={Path.Combine(Paths.Local, "app.db")};Default Timeout=5"));
        return services;
    }

    /// <summary>啟動期套用結構定義檔一次。</summary>
    public static async Task InitializeVncscpSchemaAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        var dbFactory = services.GetRequiredService<IDbContextFactory<VncscpDbContext>>();
        await using var dbContext = await dbFactory.CreateDbContextAsync(cancellationToken);
        await VncscpSchema.ApplyAsync(dbContext, cancellationToken);
    }
}
