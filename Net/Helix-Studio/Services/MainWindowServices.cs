using System.Diagnostics;
using System.IO;
using System.Reflection;
using Helix_Studio.Infrastructure.Utilities;
using Helix_Studio.Model.Navigation;
using Helix_Studio.Model.Notifications;
using Helix_Studio.Model.Settings;
using Helix_Studio.Model.Workspace;

namespace Helix_Studio.Services;

/// <summary>主視窗用：離線說明、設定與啟動頁、狀態列、工作區與全域搜尋建議。</summary>
public sealed class MainWindowServices
{
    private sealed record SearchIndexItem(string Keyword, string Tag, string Label);

    private static readonly List<SearchIndexItem> SearchIndex =
    [
        new("home","Home","Home 首頁"),
        new("首頁","Home","Home 首頁"),
        new("event log","EventLog","Event Log 事件日誌"),
        new("日誌","EventLog","Event Log 事件日誌"),
        new("log","EventLog","Event Log 事件日誌"),
        new("language","Settings","Settings › General"),
        new("介面語言","Settings","Settings › General"),
        new("start page","Settings","Settings › General"),
        new("啟動頁面","Settings","Settings › General"),
        new("inactivity","Settings","Settings › General"),
        new("閒置","Settings","Settings › General"),
        new("theme","Settings","Settings › Display"),
        new("主題","Settings","Settings › Display"),
        new("font","Settings","Settings › Display"),
        new("字型","Settings","Settings › Display"),
        new("density","Settings","Settings › Display"),
        new("密度","Settings","Settings › Display"),
        new("workspace","Settings","Settings › Workspaces"),
        new("工作區","Settings","Settings › Workspaces"),
        new("log level","Settings","Settings › Log"),
        new("記錄等級","Settings","Settings › Log"),
        new("retention","Settings","Settings › Log"),
        new("保留","Settings","Settings › Log"),
        new("export","Settings","Settings › Log"),
        new("匯出","Settings","Settings › Log"),
        new("about","Settings","Settings › About"),
        new("關於","Settings","Settings › About"),
        new("version","Settings","Settings › About"),
        new("license","Settings","Settings › About"),
        new("授權","Settings","Settings › About"),
        new("module","Modules","Modules 模組"),
        new("modules","Modules","Modules 模組"),
        new("模組","Modules","Modules 模組"),
        new("擴充","Modules","Modules 模組"),
        new("外掛","Modules","Modules 模組"),
        new("plugin","Modules","Modules 模組"),
    ];

    private readonly SettingsServices     _settingsService;
    private readonly WorkspaceServices    _workspaceService;
    private readonly NotificationsServices _notifications;

    /// <summary>以設定、工作區與通知服務建立主視窗服務。</summary>
    public MainWindowServices(
        SettingsServices settingsService,
        WorkspaceServices workspaceService,
        NotificationsServices notifications)
    {
        _settingsService  = settingsService;
        _workspaceService = workspaceService;
        _notifications    = notifications;
    }

    /// <summary>以預設瀏覽器或關聯程式開啟本機說明索引（<c>Assets\web</c>）。</summary>
    public void OpenOfflineHelp()
    {
        var path = AppPathUtility.QueryHelpIndexFilePath();
        if (!File.Exists(path))
        {
            EventLogServices.Warning("[說明] 找不到本機 index.html（Assets\\web）。", "Help");
            _notifications.Publish(
                new ShellNotificationMessage
                {
                    MessageText = "找不到本機說明檔（Assets\\web\\index.html）。",
                    Level       = ShellNotificationLevel.Warning,
                });
            return;
        }

        try
        {
            _ = Process.Start(
                new ProcessStartInfo
                {
                    FileName        = path,
                    UseShellExecute = true,
                });
        }
        catch (Exception ex)
        {
            EventLogServices.Error($"[說明] 開啟失敗：{ex.Message}", "Help");
            _notifications.Publish(
                new ShellNotificationMessage
                {
                    MessageText = "無法開啟說明檔，請聯絡系統管理員。",
                    Level       = ShellNotificationLevel.Warning,
                });
        }
    }

    /// <summary>查詢目前使用者設定快照。</summary>
    public AppSettings QueryCurrentSettings() => _settingsService.Current;

    /// <summary>內建殼層分頁之導覽標籤（不含動態模組項目）。</summary>
    public IReadOnlyList<string> QueryShellStaticRouteTags() =>
        ["Home", "EventLog", "Modules", "Settings"];

    /// <summary>依「上次造訪頁面」設定與支援的導覽標籤，決定啟動時應顯示的頁面標籤。</summary>
    public string QueryStartPageTag(IReadOnlyCollection<string> supportedTags)
    {
        var settings = _settingsService.Current;
        if (settings.StartPageIndex != 1) return "Home";
        return supportedTags.Contains(settings.LastVisitedPageTag) ? settings.LastVisitedPageTag : "Home";
    }

    /// <summary>組合狀態列顯示用的主機名稱、版本字串與使用中工作區名稱。</summary>
    public MainStatusInfo QueryStatusInfo()
    {
        var machineName = Environment.MachineName;
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        var versionText = version is not null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v—";
        var activeWorkspace = _workspaceService.GetActive()?.Name ?? "—";
        return new MainStatusInfo(machineName, versionText, activeWorkspace);
    }

    /// <summary>列舉所有已建立的工作區。</summary>
    public IReadOnlyList<WorkspaceModel> QueryAllWorkspaces() => _workspaceService.GetAll();

    /// <summary>將指定工作區設為使用中並回傳其名稱。</summary>
    public string? ActivateWorkspace(int id)
    {
        _workspaceService.SetActive(id);
        return _workspaceService.GetActive()?.Name;
    }

    /// <summary>在使用者選擇「還原上次分頁」時，更新最後造訪的導覽標籤並儲存。</summary>
    public void PersistLastVisited(string tag)
    {
        var settings = _settingsService.Current;
        if (settings.StartPageIndex != 1) return;
        if (string.Equals(settings.LastVisitedPageTag, tag, StringComparison.Ordinal)) return;
        settings.LastVisitedPageTag = tag;
        _settingsService.SaveSettings(settings);
    }

    /// <summary>依內建關鍵字索引產生主視窗搜尋的建議清單。</summary>
    public IReadOnlyList<MainSearchSuggestion> QuerySearchSuggestions(string query)
    {
        var q = query.Trim();
        if (string.IsNullOrEmpty(q)) return [];

        return SearchIndex
            .Where(x => x.Keyword.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || x.Label.Contains(q, StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.Label)
            .Select(g => new MainSearchSuggestion(g.First().Tag, g.Key))
            .Take(10)
            .ToList();
    }

    /// <summary>從單一搜尋字串推斷應導向的內部導覽標籤（若無則為 <c>null</c>）。</summary>
    public string? QueryNavigateTagFromSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        if (query.Contains("log", StringComparison.OrdinalIgnoreCase) || query.Contains("日誌", StringComparison.OrdinalIgnoreCase))
            return "EventLog";
        if (query.Contains("模組", StringComparison.Ordinal) || query.Contains("擴充", StringComparison.Ordinal)
         || query.Contains("module", StringComparison.OrdinalIgnoreCase) || query.Contains("plugin", StringComparison.OrdinalIgnoreCase))
            return "Modules";

        var match = SearchIndex.FirstOrDefault(x => x.Keyword.Contains(query, StringComparison.OrdinalIgnoreCase));
        return match?.Tag;
    }
}


