using System;
using System.IO;

namespace Helix_Studio.Utils;

/// <summary>
/// 路徑工具（集中管理本機資料夾位置）。
/// </summary>
public static class AppPathHelper
{
    /// <summary>
    /// 取得應用程式資料根目錄（LocalAppData）。
    /// </summary>
    public static string QueryAppDataDirectory()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HorizonNova",
            "Helix-Studio");

        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// 取得設定檔（INI）路徑。
    /// </summary>
    public static string QueryConfigFilePath()
        => Path.Combine(QueryAppDataDirectory(), "Helix-Studio.ini");

    /// <summary>
    /// 取得工作區根目錄。
    /// </summary>
    public static string QueryWorkspacesDirectory()
    {
        var path = Path.Combine(QueryAppDataDirectory(), "Workspaces");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// 取得日誌根目錄。
    /// </summary>
    public static string QueryLogsDirectory()
    {
        var path = Path.Combine(QueryAppDataDirectory(), "Logs");
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
}
