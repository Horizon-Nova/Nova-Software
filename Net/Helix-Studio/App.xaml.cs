using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Helix_Studio.Data;
using Helix_Studio.Services;
using Helix_Studio.Services.Tools;
using Helix_Studio.ViewModels;
using Helix_Studio.Views;

namespace Helix_Studio;

public partial class App : Application
{
    private SystemTrayService? _tray;
    private ThemeService? _themeService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddSingleton<AppSettingsService>();
        services.AddSingleton<StartupSettingsService>();
        services.AddSingleton<ToolCatalogService>();
        services.AddSingleton<InstalledToolsRepository>();
        services.AddSingleton<WorkspacesRepository>();
        services.AddSingleton<EventLogService>();
        services.AddSingleton<ToolInstallService>();
        services.AddSingleton<ToolProcessService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<MainViewModel>();

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<AppSettingsService>().Initialize();

        _themeService = provider.GetRequiredService<ThemeService>();
        _themeService.Initialize();

        var vm = provider.GetRequiredService<MainViewModel>();
        var settings = provider.GetRequiredService<AppSettingsService>();
        var processService = provider.GetRequiredService<ToolProcessService>();

        // FL-17: 系統匣
        _tray = new SystemTrayService(processService);
        _tray.RestoreRequested += () => Current?.MainWindow?.Dispatcher.Invoke(() =>
        {
            if (Current.MainWindow is MainWindow w) { w.Show(); w.WindowState = WindowState.Normal; w.Activate(); }
        });
        _tray.ExitRequested += () => Current?.MainWindow?.Dispatcher.Invoke(() =>
        {
            vm.SaveLastRunningTools();
            _tray?.HideIcon();
            _tray?.Dispose();
            Current.Shutdown();
        });
        _tray.QuickLaunchRequested += toolId =>
        {
            var tool = vm.AppList.Apps.FirstOrDefault(t => t.Id == toolId);
            if (tool is not null)
                vm.AppList.StartCommand.Execute(tool);
        };
        _tray.QuickStopRequested += toolId =>
        {
            var tool = vm.AppList.Apps.FirstOrDefault(t => t.Id == toolId);
            if (tool is not null)
                vm.AppList.StopCommand.Execute(tool);
        };
        _tray.ShowIcon();

        var window = new MainWindow { DataContext = vm };

        _tray.Attach(window);

        // FL-16: 啟動最小化
        if (settings.Settings.StartMinimized)
        {
            window.Show();
            _tray.MinimizeToTray();
        }
        else
        {
            window.Show();
        }

        // 讓系統匣右鍵選單在需要時更新
        window.Activated += (_, _) => RefreshTrayMenu(vm, _tray);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.HideIcon();
        _tray?.Dispose();
        _themeService?.Dispose();
        base.OnExit(e);
    }

    private static void RefreshTrayMenu(MainViewModel vm, SystemTrayService tray)
    {
        var tools = vm.AppList.Apps.Select(t => (t.Id, t.Name, t.Status == Models.AppStatus.Running));
        tray.RefreshContextMenu(tools);
    }
}
