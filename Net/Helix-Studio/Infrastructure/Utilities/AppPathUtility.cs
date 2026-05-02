using System.IO;

namespace Helix_Studio.Infrastructure.Utilities;

/// <summary>應用程式資料目錄與常用檔案路徑查詢。</summary>
public static class AppPathUtility
{
    /// <summary>查詢並建立本機應用程式資料根目錄。</summary>
    public static string QueryAppDataDirectory()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HorizonNova",
            "Helix-Studio");

        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>INI 組態檔完整路徑。</summary>
    public static string QueryConfigFilePath() =>
        Path.Combine(QueryAppDataDirectory(), "Helix-Studio.ini");

    /// <summary>模組載入狀態 JSON 路徑。</summary>
    public static string QueryModuleStateFilePath() =>
        Path.Combine(QueryAppDataDirectory(), "module-state.json");

    /// <summary>查詢並建立使用者模組安裝根目錄。</summary>
    public static string QueryModulesDirectory()
    {
        var path = Path.Combine(QueryAppDataDirectory(), "Modules");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>指定工作區下之模組覆寫目錄。</summary>
    public static string QueryWorkspaceModulesDirectory(int workspaceId)
    {
        var path = Path.Combine(
            QueryAppDataDirectory(),
            "Workspaces",
            workspaceId.ToString(),
            "Modules");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>指定模組之資料目錄（隔離於模組識別碼）。</summary>
    public static string QueryModuleDataDirectory(string moduleId)
    {
        var safeModuleId = string.IsNullOrWhiteSpace(moduleId) ? "unknown-module" : moduleId.Trim();
        var path = Path.Combine(QueryAppDataDirectory(), "ModuleData", safeModuleId);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>模組設定 JSON 檔完整路徑。</summary>
    public static string QueryModuleSettingsFilePath(string moduleId) =>
        Path.Combine(QueryModuleDataDirectory(moduleId), "settings.json");

    /// <summary>離線說明首頁 HTML 之完整路徑。</summary>
    public static string QueryHelpIndexFilePath() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Assets", "web", "index.html"));
}
