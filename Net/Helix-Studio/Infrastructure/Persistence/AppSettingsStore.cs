using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Helix_Studio.Model.Settings;

namespace Helix_Studio.Infrastructure.Persistence;

/// <summary>應用程式使用者設定之 JSON 檔案持久化。</summary>
public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly string AppDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HorizonNovaStudio");

    private static readonly string SettingsFile = Path.Combine(AppDataDir, "settings.json");

    /// <summary>嘗試載入設定檔；不存在或失敗時回傳 null。</summary>
    public AppSettings? TryLoad()
    {
        try
        {
            if (!File.Exists(SettingsFile))
            {
                return null;
            }

            var json = File.ReadAllText(SettingsFile);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>將設定寫入 JSON 檔。</summary>
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(AppDataDir);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsFile, json);
    }
}
