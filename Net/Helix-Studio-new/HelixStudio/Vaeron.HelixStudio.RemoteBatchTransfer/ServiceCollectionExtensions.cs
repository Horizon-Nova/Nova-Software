using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.Internal.Runtime;
using Vaeron.HelixStudio.RemoteBatchTransfer.Internal;
using Vaeron.HelixStudio.RemoteBatchTransfer.Internal.Models;
using Vaeron.Kernel;

namespace Vaeron.HelixStudio.RemoteBatchTransfer;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRemoteBatchTransferFeature(this IServiceCollection services)
    {
        services.AddDbContextFactory<RemoteBatchTransferDbContext>((_, o) =>
            o.UseSqlite($"Data Source={Path.Combine(Paths.Local, "app.db")}"));

        services.AddSingleton<RemoteBatchTransferStore>();
        services.AddSingleton<RemoteTransferStore>();
        services.AddSingleton<LocalFileSystemStore>();
        services.AddSingleton<RemoteBatchTransferService>();
        services.AddSingleton<RemoteBatchTransferFacade>();
        services.AddSingleton<IRemoteBatchTransferFacade>(sp =>
            FacadeAuditProxy.Wrap<IRemoteBatchTransferFacade>(
                sp.GetRequiredService<RemoteBatchTransferFacade>(), sp.GetServices<IAuditSink>()));
        services.AddSingleton<IFeatureDatabaseInitializer, RemoteBatchTransferDatabaseInitializer>();
        services.AddTransient<RemoteBatchTransferViewModel>();
        services.AddTransient<RemoteBatchTransferView>();
        return services;
    }
}
