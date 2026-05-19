using Helix_Studio.AppHost;
using Helix_Studio.Data.Repositories;
using Helix_Studio.Domain;
using Helix_Studio.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Helix_Studio.Extensions;

/// <summary>
/// DI 註冊管理。
/// </summary>
public static class ServiceExtensions
{
    /// <summary>
    /// DI 註冊管理 AppHost。
    /// </summary>
    public static IServiceCollection AppHostModule(this IServiceCollection services)
    {
        services.AddSingleton<ApplicationRuntime>();
        services.AddSingleton<NavigationRuntime>();
        services.AddSingleton<AppearanceRuntime>();
        services.AddSingleton<LocalizationRuntime>();
        services.AddSingleton<NotificationRuntime>();
        services.AddSingleton<InactivityRuntime>();
        return services;
    }

    /// <summary>
    /// DI 註冊管理 ViewModels。
    /// </summary>
    public static IServiceCollection ViewModelsModule(this IServiceCollection services)
    {
        services.AddTransient<MainWindowWindowViewModel>();
        services.AddTransient<OverviewPageViewModel>();
        services.AddTransient<EventLogPageViewModel>();
        services.AddTransient<ModulesPageViewModel>();
        services.AddTransient<SettingsPageViewModel>();
        return services;
    }

    /// <summary>
    /// DI 註冊管理 Domain。
    /// </summary>
    public static IServiceCollection DomainModule(this IServiceCollection services)
    {
        services.AddSingleton<MainWindowDomain>();
        services.AddSingleton<OverviewDomain>();
        services.AddSingleton<EventLogDomain>();
        services.AddSingleton<ModulesDomain>();
        services.AddSingleton<SettingsDomain>();
        return services;
    }

    /// <summary>
    /// DI 註冊管理 Data。
    /// </summary>
    public static IServiceCollection DataModule(this IServiceCollection services)
    {
        services.AddSingleton<IniRepository>();
        services.AddSingleton<WorkspacesRepository>();
        services.AddSingleton<ModulesRepository>();
        services.AddSingleton<EventLogRepository>();
        return services;
    }
}
