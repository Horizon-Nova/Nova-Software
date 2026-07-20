namespace Vaeron.SharedKernel;

public sealed class AppDataPath(string appName)
{
    public string RoamingRoot
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vaeron", appName);

    public string LocalRoot
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vaeron", appName);

    public string ConfigFilePath => Path.Combine(RoamingRoot, "config.json");
    public string DatabasePath => Path.Combine(LocalRoot, "app.db");
    public string SecretsPath => Path.Combine(LocalRoot, "secrets.bin");
    public string LogDirectory => Path.Combine(LocalRoot, "logs");
    public string CacheDirectory => Path.Combine(LocalRoot, "cache");

    /// <summary>建立完整目錄結構</summary>
    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(RoamingRoot);
        Directory.CreateDirectory(LocalRoot);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(CacheDirectory);
    }
}
