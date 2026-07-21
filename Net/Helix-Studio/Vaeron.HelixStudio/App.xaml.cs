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

        var settingsFacade = _host.Services.GetRequiredService<SettingsFacade>();
        await ApplySavedThemeAsync();
        await settingsFacade.ApplyReconcileStartupRegistrationAsync();

        var eventLogFacade = _host.Services.GetRequiredService<Features.EventLog.EventLogFacade>();
        await eventLogFacade.ApplyRetentionPolicyAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    /// <summary>各 Feature DbContext 建表（共用 app.db，逐一 CREATE TABLE IF NOT EXISTS）</summary>
    private async Task EnsureDatabasesCreatedAsync()
    {
        var services = _host!.Services;

        await EnsureCreatedAsync(services.GetRequiredService<IDbContextFactory<WorkspaceManagementDbContext>>());
        await EnsureCreatedAsync(services.GetRequiredService<IDbContextFactory<EventLogDbContext>>());
        await EnsureColumnExistsAsync(services.GetRequiredService<IDbContextFactory<EventLogDbContext>>(), "log_settings_rows", "export_path", "TEXT");
        await EnsureCreatedAsync(services.GetRequiredService<IDbContextFactory<AppsManagementDbContext>>());
        await EnsureCreatedAsync(services.GetRequiredService<IDbContextFactory<AppLibraryDbContext>>());
        await EnsureCreatedAsync(services.GetRequiredService<IDbContextFactory<SettingsDbContext>>());
        await EnsureColumnExistsAsync(services.GetRequiredService<IDbContextFactory<SettingsDbContext>>(), "app_settings_rows", "theme_pack", "TEXT", ThemePack.DefaultId);
        await EnsureColumnExistsAsync(services.GetRequiredService<IDbContextFactory<SettingsDbContext>>(), "app_settings_rows", "background_animation", "INTEGER", "1");
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

    /// <summary>既有表補欄位。defaultLiteral 有值時補上 SQLite 的 DEFAULT 子句，讓既有列同步回填，
    /// 不是只讓「之後新寫入的列」有預設值——C# 端非 nullable 型別的屬性，讀到既有列的 NULL 會直接炸例外。</summary>
    private static async Task EnsureColumnExistsAsync<TContext>(IDbContextFactory<TContext> factory, string table, string column, string sqlType, string? defaultLiteral = null) where TContext : DbContext
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var checkCmd = db.Database.GetDbConnection().CreateCommand();
        checkCmd.CommandText = $"PRAGMA table_info({table})";
        await db.Database.OpenConnectionAsync();
        await using var reader = await checkCmd.ExecuteReaderAsync();
        var exists = false;
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(reader.GetOrdinal("name")), column, StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                break;
            }
        }
        await reader.DisposeAsync();
        if (!exists)
        {
            var defaultClause = defaultLiteral is null ? "" : string.Concat(" DEFAULT '", defaultLiteral, "'");
            var alterSql = string.Concat("ALTER TABLE ", table, " ADD COLUMN ", column, " ", sqlType, defaultClause);
            await db.Database.ExecuteSqlRawAsync(alterSql);
        }

        // 保險回填：即使欄位早就存在，也可能是先前某次啟動在補上 DEFAULT 子句之前就已經新增過，留下 NULL 值
        if (defaultLiteral is not null)
        {
            var backfillSql = string.Concat("UPDATE ", table, " SET ", column, " = '", defaultLiteral, "' WHERE ", column, " IS NULL");
            await db.Database.ExecuteSqlRawAsync(backfillSql);
        }
    }

    /// <summary>套用上次選用的主題包；該包已被移除時退回預設樣式庫，不讓啟動失敗</summary>
    private async Task ApplySavedThemeAsync()
    {
        var settings = await _host!.Services.GetRequiredService<SettingsFacade>().LoadSettingsAsync();
        var themes = _host.Services.GetRequiredService<Themes.ThemePackService>();
        var pack = themes.LoadAvailablePacks().FirstOrDefault(p => p.Id == settings.ThemePackId);
        if (pack is not null) themes.Apply(pack);
    }

    /// <summary>首次啟動建立預設工作區</summary>
    private async Task EnsureDefaultWorkspaceAsync()
    {
        var workspaceFacade = _host!.Services.GetRequiredService<Features.WorkspaceManagement.WorkspaceManagementFacade>();
        var existing = await workspaceFacade.LoadWorkspaceListAsync();
        if (existing.Count == 0)
            await workspaceFacade.CreateWorkspaceAsync(new Features.WorkspaceManagement.Contracts.CreateWorkspaceRequest { Name = "生產線 A" });
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
