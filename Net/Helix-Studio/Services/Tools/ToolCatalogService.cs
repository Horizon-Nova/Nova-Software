using System.IO;
using System.Text.Json;
using Helix_Studio.Infrastructure;
using Helix_Studio.Models;

namespace Helix_Studio.Services.Tools;

/// <summary>
/// 讀取工具目錄：合併內建（出貨）與使用者匯入兩個來源的 tool.json。
/// 同 id 以使用者匯入為準。
/// </summary>
public sealed class ToolCatalogService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public IReadOnlyList<ToolManifest> LoadCatalog()
    {
        var byId = new Dictionary<string, ToolManifest>();
        // 先內建，再使用者匯入覆寫同 id
        foreach (var manifest in ReadFrom(AppPathProvider.BuiltInCatalogDir))
            byId[manifest.Id] = manifest;
        foreach (var manifest in ReadFrom(AppPathProvider.UserCatalogDir))
            byId[manifest.Id] = manifest;
        return byId.Values.ToList();
    }

    /// <summary>工具在目錄中的來源資料夾（使用者匯入優先）。</summary>
    public string ToolSourceDir(string toolId)
    {
        var userDir = Path.Combine(AppPathProvider.UserCatalogDir, toolId);
        return Directory.Exists(userDir)
            ? userDir
            : Path.Combine(AppPathProvider.BuiltInCatalogDir, toolId);
    }

    private static IEnumerable<ToolManifest> ReadFrom(string catalogDir)
    {
        if (!Directory.Exists(catalogDir)) yield break;
        foreach (var toolDir in Directory.EnumerateDirectories(catalogDir))
        {
            var manifest = TryReadManifest(Path.Combine(toolDir, "tool.json"));
            if (manifest is not null) yield return manifest;
        }
    }

    /// <summary>讀單一 manifest；格式錯誤或缺 id 視為無效工具，回傳 null（不中斷整個目錄載入）。</summary>
    public static ToolManifest? TryReadManifest(string manifestPath)
    {
        if (!File.Exists(manifestPath)) return null;
        try
        {
            var manifest = JsonSerializer.Deserialize<ToolManifest>(File.ReadAllText(manifestPath), JsonOpts);
            return string.IsNullOrWhiteSpace(manifest?.Id) ? null : manifest;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
