using System.IO;
using System.Text.Json;

namespace Vaeron.HelixStudio.AppLibrary.Internal;

internal sealed record BuiltInCatalogEntry(
    string CatalogId, string Name, string CategoryId, string Description, string Version,
    string Developer, long FileSizeBytes, string Requirements, string EntryPath, DateTimeOffset LastUpdated);

/// <summary>套件 manifest（tool.json）。內建 Catalog 與使用者匯入的 zip 套件共用同一份 schema——
/// Entry 是套件內進入點可執行檔的相對路徑，兩種來源都靠它解析出實際可啟動的檔案。</summary>
internal sealed record ToolManifestJson(
    string Id, string Name, string CategoryId, string Description, string Version,
    string Developer, string Requirements, string Entry);

/// <summary>讀取隨 exe 出貨的唯讀 Catalog 目錄</summary>
internal sealed class BuiltInCatalogStore(string catalogRoot)
{
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
            // 缺 Entry 視為格式錯誤，整筆跳過——不再用「猜檔名等於 id」頂著。
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Entry)) continue;

            var entryPath = Path.Combine(dir, manifest.Entry);
            var fileSize = File.Exists(entryPath) ? new FileInfo(entryPath).Length : 0;

            result.Add(new BuiltInCatalogEntry(
                manifest.Id, manifest.Name, manifest.CategoryId, manifest.Description, manifest.Version,
                manifest.Developer, fileSize, manifest.Requirements, entryPath,
                File.GetLastWriteTimeUtc(manifestPath)));
        }
        return result;
    }
}
