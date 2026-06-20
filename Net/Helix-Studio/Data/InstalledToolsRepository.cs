using System.IO;
using System.Text.Json;
using Helix_Studio.Infrastructure;
using Helix_Studio.Models;

namespace Helix_Studio.Data;

/// <summary>
/// 已安裝索引（installed.json）的讀寫，依工作區分檔。
/// </summary>
public sealed class InstalledToolsRepository
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public IReadOnlyList<InstalledToolRecord> Load(string workspaceId)
    {
        var path = AppPathProvider.InstalledIndex(workspaceId);
        if (!File.Exists(path)) return [];
        var records = JsonSerializer.Deserialize<List<InstalledToolRecord>>(File.ReadAllText(path), JsonOpts);
        return records ?? [];
    }

    public void Save(string workspaceId, IEnumerable<InstalledToolRecord> records)
    {
        var path = AppPathProvider.InstalledIndex(workspaceId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(records, JsonOpts));
    }
}
