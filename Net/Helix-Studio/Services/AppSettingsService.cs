using System.IO;
using System.Text.Json;
using Helix_Studio.Infrastructure;
using Helix_Studio.Models;

namespace Helix_Studio.Services;

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public AppSettings Settings { get; private set; } = new();

    public void Initialize()
    {
        var path = AppPathProvider.AppSettingsFile;
        if (!File.Exists(path))
        {
            Settings = new AppSettings();
            return;
        }
        try
        {
            Settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOpts) ?? new AppSettings();
        }
        catch (JsonException)
        {
            File.Delete(path);
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        var path = AppPathProvider.AppSettingsFile;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Settings, JsonOpts));
        File.Move(tmp, path, overwrite: true);
    }
}
