using Helix_Studio.Services;
using Helix_Studio.Infrastructure.Persistence;
using Helix_Studio.Infrastructure.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Helix_Studio.Infrastructure.Extensions;

/// <summary>Helix Studio 之 <see cref="IServiceCollection"/> 註冊擴充。</summary>
public static class ServiceExtensions
{
    /// <summary>註冊基礎設施工具與設定／授權／工作區儲存體。</summary>
    public static IServiceCollection ConfigureHnStudioUtilities(this IServiceCollection services)
    {
        services.AddSingleton<IniConfigUtility>();
        services.AddSingleton<AppSettingsStore>();
        services.AddSingleton<LicenseStateStore>();
        services.AddSingleton<WorkspaceRepository>();
        return services;
    }

    /// <summary>註冊日誌與各領域 <c>*Services</c> 單例。</summary>
    public static IServiceCollection ConfigureHnStudioServices(this IServiceCollection services)
    {
        services.AddLogging(b => b.AddDebug());
        services.AddSingleton<ConfigurationServices>();
        services.AddSingleton<WorkspaceServices>();
        services.AddSingleton<SettingsServices>();
        services.AddSingleton<NotificationsServices>();
        services.AddSingleton<LicenseServices>();
        services.AddSingleton<ModuleLicenseServices>();
        services.AddSingleton<ActivationServices>();
        services.AddSingleton<OverviewServices>();
        services.AddSingleton<MainWindowServices>();
        services.AddSingleton<ModuleRuntimeServices>();
        services.AddSingleton<ModulesCatalogServices>();
        return services;
    }
}
