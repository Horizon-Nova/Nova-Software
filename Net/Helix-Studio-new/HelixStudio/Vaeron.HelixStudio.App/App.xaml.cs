using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vaeron.HelixStudio.AppsManagement;
using Vaeron.HelixStudio.EventLog;
using Vaeron.HelixStudio.Internal.Runtime;
using Vaeron.HelixStudio.Internal.Runtime.AppRuntime;
using Vaeron.HelixStudio.Internal.Runtime.Themes;
using Vaeron.HelixStudio.Settings;
using Vaeron.HelixStudio.WorkspaceManagement;
using Vaeron.Kernel;

namespace Vaeron.HelixStudio;

/// <summary>應用程式進入點：Generic Host 組裝、DI 註冊、啟動流程</summary>
public partial class App : Application
{
    private IHost? _host;
    private PowerMenuRuntime? _powerMenu;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        Paths.Initialize("Helix-Studio");

        var builder = Host.CreateApplicationBuilder();
        Composition.Register(builder.Services);

        _host = builder.Build();
        _host.Start();

        var audit = _host.Services.GetRequiredService<AuditLogging>();
        var notification = _host.Services.GetRequiredService<NotificationRuntime>();
        ViewModelBase.UnhandledAsyncError = ex =>
        {
            audit.Log(new AuditEntry("ViewModel", "事件回呼例外", ex));
            notification.ApplyShowFailure("[失敗] 資料載入失敗");
        };

        await EnsureDatabasesCreatedAsync();
        await EnsureDefaultWorkspaceAsync();

        var settingsFacade = _host.Services.GetRequiredService<ISettingsFacade>();
        await ApplySavedThemeAsync();
        await settingsFacade.ApplyReconcileStartupRegistrationAsync();
        await settingsFacade.ApplyReconcilePowerSettingsAsync();

        var eventLogFacade = _host.Services.GetRequiredService<IEventLogFacade>();
        await eventLogFacade.ApplyRetentionPolicyAsync();

        _powerMenu = _host.Services.GetRequiredService<PowerMenuRuntime>();
        _powerMenu.PowerMenuRequested += OnPowerMenuRequested;

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    /// <summary>各 Feature 自己的 IFeatureDatabaseInitializer 建表／欄位遷移（共用 app.db）</summary>
    private async Task EnsureDatabasesCreatedAsync()
    {
        var initializers = _host!.Services.GetServices<IFeatureDatabaseInitializer>();
        foreach (var initializer in initializers)
            await initializer.InitializeAsync();
    }

    /// <summary>套用上次選用的主題包；該包已被移除時退回預設樣式庫，不讓啟動失敗</summary>
    private async Task ApplySavedThemeAsync()
    {
        var settings = await _host!.Services.GetRequiredService<ISettingsFacade>().LoadSettingsAsync();
        var themes = _host.Services.GetRequiredService<ThemePackService>();
        var pack = themes.LoadAvailablePacks().FirstOrDefault(p => p.Id == settings.ThemePackId);
        if (pack is not null) themes.Apply(pack);
    }

    /// <summary>首次啟動建立預設工作區</summary>
    private async Task EnsureDefaultWorkspaceAsync()
    {
        var workspaceFacade = _host!.Services.GetRequiredService<IWorkspaceManagementFacade>();
        var existing = await workspaceFacade.LoadWorkspaceListAsync();
        if (existing.Count == 0)
            await workspaceFacade.CreateWorkspaceAsync(new CreateWorkspaceRequest { Name = "生產線 A" });
    }

    /// <summary>全域未處理例外處理</summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var audit = _host?.Services.GetService<AuditLogging>();
        audit?.Log(new AuditEntry("App", $"未處理的例外：{e.Exception.GetType().Name} - {e.Exception.Message}", e.Exception));
        e.Handled = true;
    }

    private static Task<PowerMenuAction> OnPowerMenuRequested()
    {
        var dialog = new PowerMenuWindow();
        dialog.ShowDialog();
        return Task.FromResult(dialog.SelectedAction);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        var appsManagement = _host?.Services.GetService<IAppsManagementFacade>();
        appsManagement?.ApplyDisposeAllRunningProcesses();

        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        if (_powerMenu is not null) _powerMenu.PowerMenuRequested -= OnPowerMenuRequested;

        _host?.StopAsync().GetAwaiter().GetResult();
        _host?.Dispose();
        base.OnExit(e);
    }
}
