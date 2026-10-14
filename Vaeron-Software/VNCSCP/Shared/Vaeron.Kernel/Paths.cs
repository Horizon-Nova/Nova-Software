namespace Vaeron.Kernel;

/// <summary>路徑唯一解析點。single-file 發佈時組件位置回傳空字串,故路徑不得由組件位置推導。</summary>
public static class Paths
{
    private static string? _appName;

    public static void Initialize(string appName) => _appName = appName;

    private static string AppName => _appName
        ?? throw new InvalidOperationException("Paths.Initialize 未於啟動時呼叫");

    /// <summary>可漫遊的使用者資料：設定、憑證。</summary>
    public static string Roaming => Ensure(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName));

    /// <summary>本機使用者資料：快取、日誌、大型資產。</summary>
    public static string Local => Ensure(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName));

    public static string Logs => Ensure(Path.Combine(Local, "Logs"));

    public static string Cache => Ensure(Path.Combine(Local, "Cache"));

    public static string Assets => Ensure(Path.Combine(Local, "Assets"));

    /// <summary>安裝目錄。唯讀,不得寫入。</summary>
    public static string InstallDirectory => AppContext.BaseDirectory;

    private static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
