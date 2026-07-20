using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vaeron.HelixStudio.Features.AppLibrary;
using Vaeron.HelixStudio.Features.AppLibrary.Data;
using Vaeron.HelixStudio.Features.AppsManagement;
using Vaeron.HelixStudio.Features.AppsManagement.Data;
using Vaeron.HelixStudio.Features.EventLog;
using Vaeron.HelixStudio.Features.EventLog.Data;
using Vaeron.HelixStudio.Features.RemoteBatchTransfer;
using Vaeron.HelixStudio.Features.RemoteBatchTransfer.Data;
using Vaeron.HelixStudio.Features.Settings;
using Vaeron.HelixStudio.Features.Settings.Data;
using Vaeron.HelixStudio.Features.UsbVoltageControl;
using Vaeron.HelixStudio.Features.UsbVoltageControl.Data;
using Vaeron.HelixStudio.Features.WorkspaceManagement;
using Vaeron.HelixStudio.Features.WorkspaceManagement.Data;
using Vaeron.HelixStudio.Themes;
using Vaeron.Platform;
using Vaeron.SharedKernel;

namespace Vaeron.HelixStudio;

/// <summary>應用程式進入點：Generic Host 組裝、DI 註冊、啟動流程</summary>
public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var appDataPath = new AppDataPath("Helix-Studio");
        appDataPath.EnsureDirectoriesExist();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddPlatform(appDataPath);
        builder.Services.AddWorkspaceManagementFeature();
        builder.Services.AddEventLogFeature();
        builder.Services.AddAppsManagementFeature();
        builder.Services.AddAppLibraryFeature();
        builder.Services.AddSettingsFeature();
        builder.Services.AddUsbVoltageControlFeature();
        builder.Services.AddRemoteBatchTransferFeature();
        builder.Services.AddShell();

        _host = builder.Build();
        _host.Start();

        var audit = _host.Services.GetRequiredService<AuditLogging>();
        var notification = _host.Services.GetRequiredService<Vaeron.Platform.AppRuntime.NotificationRuntime>();
        ViewModelBase.UnhandledAsyncError = ex =>
        {
            audit.Log("ViewModel", "事件回呼例外", ex);
            notification.ApplyShowResult(false, $"{ResultContract.FailPrefix}資料載入失敗");
        };

        await EnsureDatabasesCreatedAsync();
        await EnsureDefaultWorkspaceAsync();
        await ApplyInitialThemeAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    /// <summary>各 Feature DbContext 建表（共用 app.db，逐一 CREATE TABLE IF NOT EXISTS）</summary>
    private async Task EnsureDatabasesCreatedAsync()
    {
        var services = _host!.Services;

        await EnsureCreatedAsync(services.GetRequiredService<IDbContextFactory<WorkspaceManagementDbContext>>());
        await EnsureCreatedAsync(services.GetRequiredService<IDbContextFactory<EventLogDbContext>>());
        await EnsureCreatedAsync(services.GetRequiredService<IDbContextFactory<AppsManagementDbContext>>());
        await EnsureCreatedAsync(services.GetRequiredService<IDbContextFactory<AppLibraryDbContext>>());
        await EnsureCreatedAsync(services.GetRequiredService<IDbContextFactory<SettingsDbContext>>());
        await EnsureCreatedAsync(services.GetRequiredService<IDbContextFactory<UsbVoltageControlDbContext>>());
        await EnsureCreatedAsync(services.GetRequiredService<IDbContextFactory<RemoteBatchTransferDbContext>>());
    }

    private static async Task EnsureCreatedAsync<TContext>(IDbContextFactory<TContext> factory) where TContext : DbContext
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.OpenConnectionAsync();
        var script = db.Database.GenerateCreateScript().Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ");
        await db.Database.ExecuteSqlRawAsync(script);
    }

    /// <summary>首次啟動建立預設工作區</summary>
    private async Task EnsureDefaultWorkspaceAsync()
    {
        var workspaceFacade = _host!.Services.GetRequiredService<Features.WorkspaceManagement.WorkspaceManagementFacade>();
        var existing = await workspaceFacade.LoadWorkspaceListAsync();
        if (existing.Count == 0)
            await workspaceFacade.CreateWorkspaceAsync(new Features.WorkspaceManagement.Contracts.CreateWorkspaceRequest { Name = "生產線 A" });
    }

    private async Task ApplyInitialThemeAsync()
    {
        var settingsFacade = _host!.Services.GetRequiredService<SettingsFacade>();
        var settings = await settingsFacade.LoadSettingsAsync();
        var isDark = settings.ThemeMode == Features.Settings.Contracts.ThemeMode.Dark;

        AccentColorService.Apply(Resources, settings.AccentHue, settings.AccentChroma, isDark);

        if (isDark)
        {
            var existing = Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source?.OriginalString.Contains("Themes/Tokens.Light.xaml") == true);
            if (existing is not null)
            {
                var index = Resources.MergedDictionaries.IndexOf(existing);
                Resources.MergedDictionaries[index] = new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Vaeron.HelixStudio;component/Themes/Tokens.Dark.xaml"),
                };
            }
        }
    }

    /// <summary>全域未處理例外處理</summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var audit = _host?.Services.GetService<AuditLogging>();
        audit?.Log("App", $"未處理的例外：{e.Exception.GetType().Name} - {e.Exception.Message}", e.Exception);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        var toolProcess = _host?.Services.GetService<Features.AppsManagement.Data.ToolProcessRepository>();
        toolProcess?.DisposeAllRunning();

        _host?.StopAsync().GetAwaiter().GetResult();
        _host?.Dispose();
        base.OnExit(e);
    }
}
