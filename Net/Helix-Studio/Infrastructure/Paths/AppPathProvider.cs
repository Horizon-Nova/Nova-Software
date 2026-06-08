using System;
using System.IO;

namespace Helix_Studio.Infrastructure.Paths;

/// <summary>
/// 應用程式路徑提供者，集中管理 App Home Root 與相關檔案位置。
/// </summary>
public static class AppPathProvider
{
    /// <summary>
    /// 取得應用程式資料根目錄（App Home Root）。
    /// </summary>
    public static string QueryAppDataDirectory()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".vaeron",
            "Helix-Studio");

        EnsureAppHomeRoot(path);
        return path;
    }

    /// <summary>
    /// 取得設定檔路徑。
    /// </summary>
    public static string QueryConfigFilePath()
        => Path.Combine(QueryAppDataDirectory(), "config.toml");

    /// <summary>
    /// 取得狀態資料庫路徑。
    /// </summary>
    public static string QueryStateDatabaseFilePath()
        => Path.Combine(QueryAppDataDirectory(), "state.sqlite");

    /// <summary>
    /// 取得日誌資料庫路徑。
    /// </summary>
    public static string QueryLogsDatabaseFilePath()
        => Path.Combine(QueryAppDataDirectory(), "logs.sqlite");

    /// <summary>
    /// 取得工作區根目錄。
    /// </summary>
    public static string QueryWorkspacesDirectory()
    {
        var path = Path.Combine(QueryAppDataDirectory(), "data", "workspaces");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// 取得日誌根目錄。
    /// </summary>
    public static string QueryLogsDirectory()
    {
        var path = Path.Combine(QueryAppDataDirectory(), "log");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// 取得法律聲明檔案路徑。
    /// </summary>
    public static string QueryLegalNoticesFilePath(string languageCode)
        => Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Legal",
            $"Helix-Studio-Terms.{languageCode}.txt");

    private static void EnsureAppHomeRoot(string appHomeRootPath)
    {
        Directory.CreateDirectory(appHomeRootPath);
        Directory.CreateDirectory(Path.Combine(appHomeRootPath, "cache"));
        Directory.CreateDirectory(Path.Combine(appHomeRootPath, "log"));
        Directory.CreateDirectory(Path.Combine(appHomeRootPath, "backups"));
        Directory.CreateDirectory(Path.Combine(appHomeRootPath, "sessions"));
        Directory.CreateDirectory(Path.Combine(appHomeRootPath, "tmp"));
        Directory.CreateDirectory(Path.Combine(appHomeRootPath, "data"));
        Directory.CreateDirectory(Path.Combine(appHomeRootPath, "data", "workspaces"));

        EnsureFileExists(Path.Combine(appHomeRootPath, "config.toml"), """
[Main]
Workspace=
WorkspaceDescription=

[Settings]
DataDensityIndex=0
FontScaleIndex=0
InactivityIndex=0
LanguageIndex=0
LastVisitedPageTag=
LogExportFolder=
LogLevelIndex=0
LogRetentionIndex=0
StartPageIndex=0
ThemeIndex=0
""");
        EnsureFileExists(Path.Combine(appHomeRootPath, "state.sqlite"), string.Empty);
        EnsureFileExists(Path.Combine(appHomeRootPath, "logs.sqlite"), string.Empty);
    }

    private static void EnsureFileExists(string filePath, string content)
    {
        if (File.Exists(filePath))
        {
            return;
        }

        File.WriteAllText(filePath, content);
    }
}
