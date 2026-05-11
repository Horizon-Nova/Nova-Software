using Helix_Studio.Core;
using Helix_Studio.Domain;
using Helix_Studio.Services;
using Helix_Studio.ViewModels;
using HorizonNova.HelixStudio.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Helix_Studio.Extensions;

/// <summary>
/// Helix Studio 的 DI 註冊管理。
/// </summary>
public static class ServiceExtensions
{
    /// <summary>
    /// DI 註冊管理 ViewModels。
    /// </summary>
    public static IServiceCollection ViewModelsModule(this IServiceCollection services)
    {
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<SettingsPageViewModel>();
        services.AddTransient<OverviewPageViewModel>();
        services.AddTransient<ModulesPageViewModel>();
        services.AddTransient<EventLogPageViewModel>();
        return services;
    }

    /// <summary>
    /// DI 註冊管理 Services。
    /// </summary>
    public static IServiceCollection ServicesModule(this IServiceCollection services)
    {
        services.AddSingleton<MainWindowServices>();
        services.AddSingleton<SettingsServices>();
        services.AddSingleton<OverviewServices>();
        services.AddSingleton<ModulesServices>();
        services.AddSingleton<EventLogServices>();
        return services;
    }

    /// <summary>
    /// DI 註冊管理 Domain。
    /// </summary>
    public static IServiceCollection DomainModule(this IServiceCollection services)
    {
        services.AddSingleton<MainWindowDomain>();
        services.AddSingleton<SettingsDomain>();
        services.AddSingleton<OverviewDomain>();
        services.AddSingleton<ModulesDomain>();
        services.AddSingleton<EventLogDomain>();
        return services;
    }

    /// <summary>
    /// DI 註冊管理 Core。
    /// </summary>
    public static IServiceCollection CoreModule(this IServiceCollection services)
    {
        services.AddSingleton<IniCore>();
        services.AddSingleton<LicenseCore>();
        services.AddSingleton<ModulesCore>();
        services.AddSingleton<WorkspacesCore>();
        services.AddSingleton<ThemeCore>();
        services.AddSingleton<AppearanceCore>();
        services.AddSingleton<EventLogCore>();
        services.AddSingleton<ShellHealthNotifier>();
        services.AddSingleton<IModuleHealthSink>(static provider => provider.GetRequiredService<ShellHealthNotifier>());
        services.AddSingleton<ShellContextFactory>();
        return services;
    }
}
