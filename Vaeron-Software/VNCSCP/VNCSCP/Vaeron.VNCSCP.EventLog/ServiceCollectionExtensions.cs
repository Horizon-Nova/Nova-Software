using Microsoft.Extensions.DependencyInjection;
using Vaeron.VNCSCP.EventLog.Internal;
using Vaeron.VNCSCP.Internal.Contracts;

namespace Vaeron.VNCSCP.EventLog;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventLog(this IServiceCollection services)
    {
        services.AddSingleton<EventLogStore>();
        services.AddSingleton<EventLogLogic>();
        services.AddSingleton<EventLogService>();
        services.AddSingleton<IEventRecorder>(provider => provider.GetRequiredService<EventLogService>());
        services.AddSingleton<IEventLogFacade, EventLogFacade>();
        services.AddTransient<EventLogViewModel>();
        services.AddTransient<EventLogView>();
        return services;
    }
}
