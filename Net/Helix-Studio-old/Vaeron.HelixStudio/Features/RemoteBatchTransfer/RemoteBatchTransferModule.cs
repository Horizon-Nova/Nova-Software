using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.Features.RemoteBatchTransfer.Data;
using Vaeron.SharedKernel;

namespace Vaeron.HelixStudio.Features.RemoteBatchTransfer;

public static class RemoteBatchTransferModule
{
    public static IServiceCollection AddRemoteBatchTransferFeature(this IServiceCollection services)
    {
        services.AddDbContextFactory<RemoteBatchTransferDbContext>((sp, o) =>
            o.UseSqlite($"Data Source={sp.GetRequiredService<AppDataPath>().DatabasePath}"));
        services.AddSingleton<RemoteBatchTransferRepository>();
        services.AddSingleton<RemoteTransferRepository>();
        services.AddSingleton<LocalFileSystemRepository>();
        services.AddSingleton<RemoteBatchTransferDomain>();
        services.AddTransient<RemoteBatchTransferViewModel>();
        services.AddTransient<RemoteBatchTransferView>();
        return services;
    }
}
