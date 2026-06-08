using Helix_Studio.AppRuntime;
using Helix_Studio.Data.Repositories;
using Helix_Studio.Domain;
using Helix_Studio.Infrastructure.Licensing;
using Helix_Studio.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Helix_Studio.Infrastructure.DependencyInjection;

/// <summary>
/// DI 註冊管理。
/// </summary>
public static class ServiceExtensions
{
    /// <summary>
    /// DI 註冊管理 App。
    /// </summary>
    public static IServiceCollection AddAppLayer(this IServiceCollection services)
    {
        services.AddSingleton<ApplicationRuntime>();
        services.AddSingleton<NavigationRuntime>();
        services.AddSingleton<AppearanceRuntime>();
        services.AddSingleton<LocalizationRuntime>();
        services.AddSingleton<NotificationRuntime>();
        services.AddSingleton<InactivityRuntime>();
        services.AddSingleton<LicenseVerificationService>();
        services.AddSingleton<IApplicationLicenseService, ApplicationLicenseService>();
        return services;
    }

    /// <summary>
    /// DI 註冊管理 ViewModels。
    /// </summary>
    public static IServiceCollection AddViewModelsLayer(this IServiceCollection services)
    {
        services.AddTransient<MainWindowWindowViewModel>();
        services.AddTransient<ActivationWindowViewModel>();
        services.AddTransient<OverviewPageViewModel>();
        services.AddTransient<EventLogPageViewModel>();
        services.AddTransient<ModulesPageViewModel>();
        services.AddTransient<SettingsPageViewModel>();
        return services;
    }

    /// <summary>
    /// DI 註冊管理 Domain。
    /// </summary>
    public static IServiceCollection AddDomainLayer(this IServiceCollection services)
    {
        services.AddSingleton<MainWindowDomain>();
        services.AddSingleton<OverviewDomain>();
        services.AddSingleton<EventLogDomain>();
        services.AddSingleton<ModulesDomain>();
        services.AddSingleton<SettingsDomain>();
        services.AddSingleton<Infrastructure.Modules.ModuleRuntimeService>();
        return services;
    }

    /// <summary>
    /// DI 註冊管理 Data。
    /// </summary>
    public static IServiceCollection AddDataLayer(this IServiceCollection services)
    {
        services.AddSingleton<IniRepository>();
        services.AddSingleton<WorkspacesRepository>();
        services.AddSingleton<ModulesRepository>();
        services.AddSingleton<EventLogRepository>();
        return services;
    }
}
