using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Helix_Studio.Domain;
using Helix_Studio.Infrastructure.Localization;
using Helix_Studio.Infrastructure.Persistence;
using Helix_Studio.Infrastructure.Utilities;
using Helix_Studio.Model.Configuration;
using Helix_Studio.Model.Settings;
using Helix_Studio.Model.Workspace;

namespace Helix_Studio.Services;

/// <summary>應用程式設定的載入、儲存、主題與日誌匯出，以及關於／法律聲明等輔助查詢。</summary>
public sealed class SettingsServices
{
    private readonly AppSettingsStore _store;
    private readonly WorkspaceServices _workspaceService;
    private readonly LicenseServices _licenseService;

    private AppSettings _current = new();

    /// <summary>以設定儲存、工作區與授權服務建立服務。</summary>
    public SettingsServices(
        AppSettingsStore store,
        WorkspaceServices workspaceService,
        LicenseServices licenseService)
    {
        _store = store;
        _workspaceService = workspaceService;
        _licenseService = licenseService;
    }

    /// <summary>目前記憶體內的應用程式設定快照。</summary>
    public AppSettings Current => _current;

    /// <summary>從儲存載入設定，並以目前快照再次儲存以遷移或補齊預設值。</summary>
    public void Load()
    {
        _current = _store.TryLoad() ?? new AppSettings();
        SaveSettings(_current);
    }

    /// <summary>回傳與 <see cref="Current"/> 相同的設定參考。</summary>
    public AppSettings QuerySettings() => _current;

    /// <summary>持久化應用程式設定並立即套用相關 UI 與日誌層級設定。</summary>
    public void SaveSettings(AppSettings settings)
    {
        try
        {
            _store.Save(settings);
            _current = settings;
            EventLogServices.Info("已儲存應用程式設定。", "Settings");
        }
        catch (Exception ex)
        {
            EventLogServices.Error($"儲存設定失敗：{ex.Message}", "Settings");
            System.Diagnostics.Debug.WriteLine($"[SettingsServices] 儲存設定失敗：{ex.Message}");
        }

        ApplyAndPersistUiConfig(settings);
    }

    /// <summary>同時套用應用程式層與使用者層的語系、主題、密度與日誌選項。</summary>
    public void ApplyAll(AppConfig config, AppSettings settings, bool persistUiConfig)
    {
        UiLocale.Apply(config.Ui.CultureName, persist: persistUiConfig);
        UiTheme.Apply(config.Ui.ThemeMode, persist: persistUiConfig);

        UiScale.ApplyByIndex(settings.FontScaleIndex);
        UiDensity.ApplyByIndex(settings.DataDensityIndex);

        EventLogServices.Instance.ConfigureFileLogging(
            SettingsDomain.QueryLogMinLevel(settings.LogLevelIndex),
            SettingsDomain.QueryRetentionDays(settings.LogRetentionIndex));
    }

    /// <summary>依使用者選項重算並套用語系與主題，持久化至組態並回傳最新 <see cref="AppConfig"/>。</summary>
    public AppConfig ApplyAndPersistUiConfig(AppSettings settings)
    {
        var culture = SettingsDomain.QueryCultureName(settings.LanguageIndex);
        UiLocale.Apply(culture, persist: true);

        var themeMode = SettingsDomain.QueryThemeMode(settings.ThemeIndex);
        UiTheme.Apply(themeMode, persist: true);

        UiScale.ApplyByIndex(settings.FontScaleIndex);
        UiDensity.ApplyByIndex(settings.DataDensityIndex);

        EventLogServices.Instance.ConfigureFileLogging(
            SettingsDomain.QueryLogMinLevel(settings.LogLevelIndex),
            SettingsDomain.QueryRetentionDays(settings.LogRetentionIndex));

        return AppServices.Config.QueryCurrent();
    }

    /// <summary>依閒置選項索引回傳自動登出或鎖定前的逾時。</summary>
    public TimeSpan QueryInactivityTimeout(int inactivityIndex)
        => SettingsDomain.QueryInactivityTimeout(inactivityIndex);

    /// <summary>匯出日誌預設目錄（目前為桌面下固定子資料夾）。</summary>
    public string QueryDefaultLogFolder()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Helix-Studio-Logs");

    /// <summary>將應用程式資料夾內的日誌目錄封裝為 ZIP；可指定匯出目錄，空白則用預設路徑。</summary>
    public LogExportResult ExportLogs(string exportDirectory)
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HorizonNovaStudio",
            "Logs");

        var targetDirectory = string.IsNullOrWhiteSpace(exportDirectory)
            ? QueryDefaultLogFolder()
            : exportDirectory.Trim();

        try
        {
            Directory.CreateDirectory(targetDirectory);
            var zipPath = Path.Combine(targetDirectory, $"Helix-Studio-Logs-{DateTime.Now:yyyyMMdd-HHmmss}.zip");

            if (Directory.Exists(logDirectory))
            {
                ZipFile.CreateFromDirectory(logDirectory, zipPath);
            }
            else
            {
                ZipFile.Open(zipPath, ZipArchiveMode.Create).Dispose();
            }

            return LogExportResult.Success(zipPath);
        }
        catch (Exception ex)
        {
            return LogExportResult.Failure(ex.Message);
        }
    }

    /// <summary>列舉工作區；設定頁工作區一節用。</summary>
    public IReadOnlyList<WorkspaceModel> QueryWorkspaces() => _workspaceService.GetAll();

    /// <summary>查詢目前啟用中的工作區。</summary>
    public WorkspaceModel? QueryActiveWorkspace() => _workspaceService.GetActive();

    /// <summary>建立新工作區（經名稱正規化與驗證）。</summary>
    public void CreateWorkspace(string name, string description) => _workspaceService.Create(name, description);

    /// <summary>更新指定工作區名稱與描述。</summary>
    public void UpdateWorkspace(int id, string name, string description) => _workspaceService.Update(id, name, description);

    /// <summary>將指定工作區設為啟用。</summary>
    public void ActivateWorkspace(int id) => _workspaceService.SetActive(id);

    /// <summary>刪除工作區，並在刪除目前使用中項目時切換至後備工作區（若可）。</summary>
    public bool DeleteWorkspace(int id) => _workspaceService.Delete(id);

    /// <summary>組合「關於」分頁顯示用版本、建置日與授權到期文字。</summary>
    public AboutInfo QueryAboutInfo()
    {
        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?.GetName().Version;
        var versionText = version is not null
            ? $"v{version.Major}.{version.Minor}.{version.Build}"
            : "v—";

        var assemblyPath = assembly?.Location;
        var buildDateText = assemblyPath is not null && File.Exists(assemblyPath)
            ? File.GetLastWriteTime(assemblyPath).ToString("yyyy-MM-dd")
            : "—";

        var licenseState = _licenseService.QueryCurrent();
        var licenseExpiryText = licenseState is null
            ? "—"
            : licenseState.ExpiresAtUtc.ToLocalTime().ToString("yyyy-MM-dd");

        return new AboutInfo(versionText, buildDateText, licenseExpiryText);
    }

    /// <summary>以預設程式開啟第三方聲明文字檔，若檔案不存在則回傳錯因。</summary>
    public LegalNoticeResult OpenLegalNotices()
    {
        var legalDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Legal");
        var filePath = Path.Combine(legalDirectory, "THIRD-PARTY-NOTICES.txt");
        if (!File.Exists(filePath))
        {
            return LegalNoticeResult.NotFound("找不到法律聲明檔案。\n( Assets/Legal/THIRD-PARTY-NOTICES.txt )");
        }

        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo { FileName = filePath, UseShellExecute = true });
        return LegalNoticeResult.Opened();
    }
}
