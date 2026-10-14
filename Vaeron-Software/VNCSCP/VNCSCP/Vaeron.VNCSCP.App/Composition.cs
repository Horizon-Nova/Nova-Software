using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vaeron.Kernel.Data;
using Vaeron.VNCSCP.ConnectionManagement;
using Vaeron.VNCSCP.Dashboard;
using Vaeron.VNCSCP.EventLog;
using Vaeron.VNCSCP.Internal.Shell;
using Vaeron.VNCSCP.RemoteDeployment;

namespace Vaeron.VNCSCP.App;

/// <summary>組裝的唯一位置。只有註冊，沒有判斷。</summary>
internal static class Composition
{
    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddVncscpData();
        services.AddEventLog();
        services.AddConnectionManagement();
        services.AddRemoteDeployment(configuration);
        services.AddDashboard();

        services.AddSingleton<ShellCoordinator>();
        services.AddSingleton<IConfirmationService>(provider => provider.GetRequiredService<ShellCoordinator>());
        services.AddSingleton<INotificationService>(provider => provider.GetRequiredService<ShellCoordinator>());
        services.AddSingleton<INavigator>(provider => provider.GetRequiredService<ShellCoordinator>());

        services.AddTransient<MainWindow>();
    }
}

