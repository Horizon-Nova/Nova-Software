using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Helix_Studio.Data;
using Helix_Studio.Infrastructure;
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
        services.AddSingleton<LicenseService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<MainViewModel>();

        var provider = services.BuildServiceProvider();

        var appSettings = provider.GetRequiredService<AppSettingsService>();
        appSettings.Initialize();

        // FL-08 N-001: 依儲存的語言設定套用介面語言（即時切換的初始套用）
        LocalizationService.Instance.SetLanguage(appSettings.Settings.Language);

        _themeService = provider.GetRequiredService<ThemeService>();
        _themeService.Initialize();

        // FL-13 N-001：授權金鑰驗證（在主視窗建立前執行）
        var licenseService = provider.GetRequiredService<LicenseService>();
        if (!RunLicenseCheck(licenseService))
        {
            Shutdown();
            return;
        }

        var vm = provider.GetRequiredService<MainViewModel>();
        var settings = provider.GetRequiredService<AppSettingsService>();
        var processService = provider.GetRequiredService<ToolProcessService>();

        // FL-09: 系統匣
        _tray = new SystemTrayService(processService, settings);
        _tray.RestoreRequested += () => Current?.MainWindow?.Dispatcher.Invoke(() =>
        {
            if (Current.MainWindow is MainWindow w) { w.Show(); w.WindowState = WindowState.Normal; w.Activate(); }
        });
        _tray.ExitRequested += () => Current?.Dispatcher.Invoke(() => Current.Shutdown());
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

        // FL-08 N-002: 啟動最小化
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

        // FL-08 N-001: 語言即時切換後重建系統匣選單，使其文字反映當前語言
        LocalizationService.Instance.LanguageChanged += () => RefreshTrayMenu(vm, _tray);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        (MainWindow?.DataContext as ViewModels.MainViewModel)?.SaveLastRunningTools();
        _tray?.HideIcon();
        _tray?.Dispose();
        _themeService?.Dispose();
        base.OnExit(e);
    }

    // ── FL-13 N-001：授權驗證流程 ────────────────────────

    private static bool RunLicenseCheck(LicenseService licenseService)
    {
        bool licensed = File.Exists(AppPathProvider.LicenseFile) && licenseService.Initialize();
        if (licensed) return true;

        bool isRebind = licenseService.PlatformKeyStatus == "rebind";
        var vm     = new LicensingViewModel(licenseService, isRebind);
        var window = new LicensingWindow { DataContext = vm };

        bool? result = window.ShowDialog();
        return result == true;
    }

    // ─────────────────────────────────────────────────────

    private static void RefreshTrayMenu(MainViewModel vm, SystemTrayService tray)
    {
        var tools = vm.AppList.Apps.Select(t => (t.Id, t.Name, t.IsRunning));
        tray.RefreshContextMenu(tools);
    }
}
