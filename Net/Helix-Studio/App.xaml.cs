using System;
using System.Windows;
using System.Threading.Tasks;
using Helix_Studio.Core;
using Helix_Studio.Domain;
using Helix_Studio.Extensions;
using Helix_Studio.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Helix_Studio;

/// <summary>WPF 應用程式入口。</summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    /// <summary>取得 DI 容器。</summary>
    public ServiceProvider QueryServiceProvider()
        => _serviceProvider ?? throw new InvalidOperationException("DI 尚未完成初始化。");

    /// <summary>啟動應用程式並建立主視窗。</summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services
            .ViewModelsModule()
            .ServicesModule()
            .DomainModule()
            .CoreModule();

        _serviceProvider = services.BuildServiceProvider();
        _serviceProvider.GetRequiredService<AppearanceCore>();
        ApplyUnhandledExceptionCapture(_serviceProvider);

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    /// <summary>結束應用程式並釋放 DI 資源。</summary>
    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        _serviceProvider = null;
        base.OnExit(e);
    }

    private static void ApplyUnhandledExceptionCapture(ServiceProvider provider)
    {
        var domain = provider.GetRequiredService<EventLogDomain>();

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                domain.RecordUnhandledException("AppDomain", exception);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            domain.RecordUnhandledException("TaskScheduler", args.Exception);
            args.SetObserved();
        };
    }
}
