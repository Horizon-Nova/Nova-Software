using System;
using System.IO;

namespace Helix_Studio.Core;

/// <summary>
/// Helix Studio 的應用程式路徑查詢核心。
/// </summary>
public static class AppPathCore
{
    /// <summary>
    /// 查詢應用程式的 LocalAppData 根目錄。
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
    /// 查詢設定檔 INI 路徑。
    /// </summary>
    public static string QueryConfigFilePath()
        => Path.Combine(QueryAppDataDirectory(), "Helix-Studio.ini");

    /// <summary>
    /// 查詢工作區根目錄。
    /// </summary>
    public static string QueryWorkspacesDirectory()
    {
        var path = Path.Combine(QueryAppDataDirectory(), "Workspaces");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// 查詢日誌根目錄。
    /// </summary>
    public static string QueryLogsDirectory()
    {
        var path = Path.Combine(QueryAppDataDirectory(), "Logs");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// 查詢法律文件路徑。
    /// </summary>
    /// <param name="languageCode">語言代碼。</param>
    public static string QueryLegalNoticesFilePath(string languageCode)
        => Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Legal",
            $"Helix-Studio-Terms.{languageCode}.txt");
}
