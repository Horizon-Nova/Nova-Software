using System.IO;
using System.Text.Json;

namespace Vaeron.HelixStudio.Features.AppLibrary.Data;

public sealed record BuiltInCatalogEntry(
    string CatalogId, string Name, string CategoryId, string Description, string Version,
    string Developer, long FileSizeBytes, string Requirements, string EntryPath, DateTimeOffset LastUpdated);

/// <summary>讀取隨 exe 出貨的唯讀 Catalog 目錄</summary>
public sealed class BuiltInCatalogRepository(string catalogRoot)
{
    private sealed record ToolManifestJson(
        string Id, string Name, string CategoryId, string Description, string Version,
        string Developer, string Requirements);

    public IReadOnlyList<BuiltInCatalogEntry> QueryBuiltInCatalog()
    {
        if (!Directory.Exists(catalogRoot)) return [];

        var result = new List<BuiltInCatalogEntry>();
        foreach (var dir in Directory.GetDirectories(catalogRoot))
        {
            var manifestPath = Path.Combine(dir, "tool.json");
            if (!File.Exists(manifestPath)) continue;

            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<ToolManifestJson>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (manifest is null) continue;

            var entryPath = Path.Combine(dir, $"{manifest.Id}.exe");
            var fileSize = File.Exists(entryPath) ? new FileInfo(entryPath).Length : 0;

            result.Add(new BuiltInCatalogEntry(
                manifest.Id, manifest.Name, manifest.CategoryId, manifest.Description, manifest.Version,
                manifest.Developer, fileSize, manifest.Requirements, entryPath,
                File.GetLastWriteTimeUtc(manifestPath)));
        }
        return result;
    }
}
