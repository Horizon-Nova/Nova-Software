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

/// <summary>Generic Host 輕量整合（solution-structure.md）：Host 只提供 DI／組態，
/// 不接管 WPF 生命週期——host.Start() 取得服務，視窗生命週期仍由 WPF Application 自己管理。</summary>
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

    /// <summary>configuration.md「必須」條款：每個 Feature 的 DbContext 各自呼叫
    /// Database.EnsureCreated() 建自己的表。全部 7 個 Feature 共用同一個實體 app.db 檔案，
    /// 這裡刻意不直接呼叫 DbContext.Database.EnsureCreatedAsync()——那個 API 在 SQLite
    /// provider 上是用「這個檔案裡有沒有『任何』資料表」當判斷依據，不是「這個 DbContext
    /// 自己的表存不存在」；7 個 DbContext 共用同一個檔案時，第一個呼叫會建好自己的表，
    /// 之後其餘 6 個呼叫都會看到「檔案已經有表了」而整批跳過，不會建自己的表，
    /// 造成後面的 Feature 第一次查詢直接撞「no such table」（實測撞到這個問題）。
    /// 改用 GenerateCreateScript() 產生這個 DbContext 自己模型的 DDL，把 "CREATE TABLE "
    /// 改寫成 "CREATE TABLE IF NOT EXISTS " 再執行——每個 DbContext 各自只建自己的表，
    /// 且對已存在的表是 no-op，重複啟動也安全。</summary>
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

    /// <summary>全新安裝時 app.db 沒有任何工作區列，側邊欄工作區切換器會綁定到 null 顯示空白
    /// （blueprint 的 seed/workspaces.js 永遠至少有一筆，這裡是真實資料庫，沒有這個保證）。
    /// 只在「一筆工作區都沒有」時建立唯一一個預設工作區並設為 active——
    /// WorkspaceManagementRepository.InsertWorkspaceAndActivateIfFirstAsync 已經處理「第一筆自動
    /// 設為 active」，這裡只需要在啟動時觸發那個路徑一次，不是額外發明新邏輯。</summary>
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

    /// <summary>App 層級最後一道防線——viewmodels.md／domain.md／repositories.md 明訂各層禁止
    /// try...catch，技術例外的唯一攔截點是 SharedKernel；但 SharedKernel 只包裝它自己白名單內的
    /// 操作，不是每一種可能的執行期例外都有機會先被它攔到（例如 P/Invoke、Clipboard 這類極少數
    /// 剩餘風險，見各 ViewModel/Repository 內的說明註解）。這裡是應用程式最外層的兜底，
    /// 記錄後不讓整個程式崩潰，跟分層的「業務語意判斷」是不同性質的兩件事。</summary>
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
