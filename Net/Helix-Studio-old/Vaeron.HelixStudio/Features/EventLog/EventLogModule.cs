using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vaeron.HelixStudio.Features.EventLog.Data;
using Vaeron.SharedKernel;

namespace Vaeron.HelixStudio.Features.EventLog;

public static class EventLogModule
{
    public static IServiceCollection AddEventLogFeature(this IServiceCollection services)
    {
        services.AddDbContextFactory<EventLogDbContext>((sp, o) =>
            o.UseSqlite($"Data Source={sp.GetRequiredService<AppDataPath>().DatabasePath}"));
        services.AddSingleton<EventLogRepository>();
        services.AddSingleton<EventLogDomain>();
        services.AddSingleton<EventLogFacade>();
        services.AddTransient<EventLogViewModel>();
        services.AddTransient<EventLogView>();
        return services;
    }
}
