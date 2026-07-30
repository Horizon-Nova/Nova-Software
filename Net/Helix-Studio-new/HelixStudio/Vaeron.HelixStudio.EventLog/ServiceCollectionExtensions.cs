using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.EventLog.Internal;
using Vaeron.HelixStudio.EventLog.Internal.Models;
using Vaeron.Kernel;

namespace Vaeron.HelixStudio.EventLog;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventLogFeature(this IServiceCollection services)
    {
        services.AddDbContextFactory<EventLogDbContext>((_, o) =>
            o.UseSqlite($"Data Source={Path.Combine(Paths.Local, "app.db")}"));

        services.AddSingleton<EventLogStore>();
        services.AddSingleton<EventLogService>();
        services.AddSingleton<EventLogFacade>();
        services.AddSingleton<Vaeron.HelixStudio.Internal.Runtime.IAuditSink, EventLogAuditSink>();
        services.AddSingleton<IEventLogFacade>(sp =>
            Vaeron.HelixStudio.Internal.Runtime.FacadeAuditProxy.Wrap<IEventLogFacade>(
                sp.GetRequiredService<EventLogFacade>(), sp.GetServices<Vaeron.HelixStudio.Internal.Runtime.IAuditSink>()));
        services.AddSingleton<Vaeron.HelixStudio.Internal.Runtime.IFeatureDatabaseInitializer, EventLogDatabaseInitializer>();
        services.AddTransient<EventLogViewModel>();
        services.AddTransient<EventLogView>();
        return services;
    }
}
