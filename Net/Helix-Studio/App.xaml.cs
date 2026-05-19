using System;
using System.Windows;
using Helix_Studio.AppHost;
using Helix_Studio.Extensions;
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

    public IServiceProvider QueryServiceProvider() => Services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services
            .AppHostModule()
            .ViewModelsModule()
            .DomainModule()
            .DataModule();

        Services = services.BuildServiceProvider(validateScopes: true);

        _ = Services.GetRequiredService<AppearanceRuntime>();

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
