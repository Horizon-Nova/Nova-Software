using System;
using System.Windows;
using Helix_Studio.AppRuntime;
using Helix_Studio.Infrastructure.DependencyInjection;
using Helix_Studio.Infrastructure.Licensing;
using Helix_Studio.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Helix_Studio;

/// <summary>
/// 應用程式入口。
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 取得目前 App 實例。
    /// </summary>
    public static new App Current => (App)Application.Current;

    /// <summary>
    /// DI 容器。
    /// </summary>
    public IServiceProvider Services { get; private set; } = new ServiceCollection().BuildServiceProvider();

    /// <summary>
    /// 取得目前應用程式的 DI 容器。
    /// </summary>
    public IServiceProvider QueryServiceProvider() => Services;

    /// <summary>
    /// 啟動應用程式並建立根服務容器。
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services
            .AddAppLayer()
            .AddViewModelsLayer()
            .AddDomainLayer()
            .AddDataLayer();

        Services = services.BuildServiceProvider(validateScopes: true);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _ = Services.GetRequiredService<AppearanceRuntime>();
        var applicationLicenseService = Services.GetRequiredService<IApplicationLicenseService>();

        if (!applicationLicenseService.VerifyStoredLicense().IsValid)
        {
            var activationWindow = new ActivationWindow();
            var activationSucceeded = activationWindow.ShowDialog() == true;
            if (!activationSucceeded || !applicationLicenseService.VerifyStoredLicense().IsValid)
            {
                Shutdown();
                return;
            }
        }

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
    }
}
