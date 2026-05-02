using Helix_Studio.Infrastructure.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Helix_Studio.Services;

/// <summary>彙整應用程式層服務的靜態存取點，並負責單次初始化 DI 與掃描模組目錄。</summary>
public static class AppServices
{
    private static bool            _initialized;
    private static ServiceProvider _provider = null!;

    /// <summary>應用程式層設定（文化、主題等）的讀寫服務。</summary>
    public static ConfigurationServices Config    { get; private set; } = null!;

    /// <summary>使用者偏好與匯出日誌等應用程式設定的服務。</summary>
    public static SettingsServices    Settings  { get; private set; } = null!;

    /// <summary>工作區的列舉、建立、刪除與啟用服務。</summary>
    public static WorkspaceServices    Workspace { get; private set; } = null!;

    /// <summary>主程式授權狀態的讀寫與啟用判斷。</summary>
    public static LicenseServices      License   { get; private set; } = null!;

    /// <summary>外掛模組授權金鑰驗證服務。</summary>
    public static ModuleLicenseServices ModuleLicense { get; private set; } = null!;

    /// <summary>啟用精靈相關的執行時期資訊與法務檔案開啟。</summary>
    public static ActivationServices   Activation { get; private set; } = null!;

    /// <summary>殼層內部通知與通知匣存取。</summary>
    public static NotificationsServices Notifications { get; private set; } = null!;

    /// <summary>首頁儀表板資料查詢。</summary>
    public static OverviewServices     OverviewPage { get; private set; } = null!;

    /// <summary>主視窗專用：說明、狀態列、搜尋建議、工作區切換等。</summary>
    public static MainWindowServices     MainWindow     { get; private set; } = null!;

    /// <summary>模組載入、啟用、卸載與執行時期內容。</summary>
    public static ModuleRuntimeServices  ModuleRuntime  { get; private set; } = null!;

    /// <summary>模組目錄掃描、匯入與已載入清單查詢。</summary>
    public static ModulesCatalogServices ModulesCatalog { get; private set; } = null!;

    /// <summary>建置 <see cref="ServiceProvider"/>、指派各靜態服務屬性、載入設定並觸發模組掃描。</summary>
    public static void Initialize()
    {
        if (_initialized) return;

        var services = new ServiceCollection();
        services.ConfigureHnStudioUtilities();
        services.ConfigureHnStudioServices();

        _provider = services.BuildServiceProvider();

        Config    = _provider.GetRequiredService<ConfigurationServices>();
        Settings  = _provider.GetRequiredService<SettingsServices>();
        Workspace = _provider.GetRequiredService<WorkspaceServices>();
        License   = _provider.GetRequiredService<LicenseServices>();
        ModuleLicense = _provider.GetRequiredService<ModuleLicenseServices>();
        Activation = _provider.GetRequiredService<ActivationServices>();
        Notifications = _provider.GetRequiredService<NotificationsServices>();
        OverviewPage = _provider.GetRequiredService<OverviewServices>();
        MainWindow     = _provider.GetRequiredService<MainWindowServices>();
        ModuleRuntime  = _provider.GetRequiredService<ModuleRuntimeServices>();
        ModulesCatalog = _provider.GetRequiredService<ModulesCatalogServices>();

        Settings.Load();
        Workspace.Initialize();

        ModulesCatalog.RequestScan();

        Workspace.WorkspaceActivated += () => ModulesCatalog.RequestScan();

        EventLogServices.Info("Helix Studio 已啟動。");
        var active = Workspace.GetActive();
        if (active is not null)
            EventLogServices.Info($"使用中的工作區：{active.Name}", "Workspace");

        _initialized = true;
    }
}

