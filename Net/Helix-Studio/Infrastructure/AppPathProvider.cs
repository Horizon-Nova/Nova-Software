using System.IO;

namespace Helix_Studio.Infrastructure;

/// <summary>
/// 應用程式所有路徑的唯一來源，避免路徑字串散落各處。
/// </summary>
public static class AppPathProvider
{
    /// <summary>應用程式安裝目錄（執行檔所在）。</summary>
    public static string AppRoot => AppContext.BaseDirectory;

    /// <summary>內建推薦工具目錄（隨程式出貨、唯讀）。</summary>
    public static string BuiltInCatalogDir => Path.Combine(AppRoot, "Catalog");

    /// <summary>App 家目錄：%USERPROFILE%\.vaeron\Helix-Studio\</summary>
    public static string Home => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".vaeron", "Helix-Studio");

    /// <summary>使用者匯入的工具目錄（可寫入），匯入的工具放這裡。</summary>
    public static string UserCatalogDir => Path.Combine(Home, "catalog");

    public static string WorkspaceDir(string workspaceId)
        => Path.Combine(Home, "workspaces", workspaceId);

    /// <summary>某工作區已安裝工具的根目錄。</summary>
    public static string InstalledToolsDir(string workspaceId)
        => Path.Combine(WorkspaceDir(workspaceId), "Tools");

    /// <summary>某工作區單一已安裝工具的目錄。</summary>
    public static string ToolDir(string workspaceId, string toolId)
        => Path.Combine(InstalledToolsDir(workspaceId), toolId);

    /// <summary>某工作區的已安裝索引檔。</summary>
    public static string InstalledIndex(string workspaceId)
        => Path.Combine(WorkspaceDir(workspaceId), "installed.json");

    /// <summary>某工作區的事件日誌檔（JSONL，逐行附加）。</summary>
    public static string EventLogFile(string workspaceId)
        => Path.Combine(WorkspaceDir(workspaceId), "eventlog.jsonl");

    /// <summary>應用程式設定檔（app-settings.json）。</summary>
    public static string AppSettingsFile => Path.Combine(Home, "app-settings.json");
}
