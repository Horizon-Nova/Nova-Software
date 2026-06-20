namespace Helix_Studio.Models;

/// <summary>
/// 工具描述檔（tool.json）的內容：工具的靜態中繼資料。
/// 工具是獨立可執行檔，Helix 以子程序執行；此處不含執行期狀態。
/// </summary>
public sealed class ToolManifest
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    /// <summary>進入點可執行檔（相對於工具資料夾）。</summary>
    public string Entry { get; init; } = string.Empty;

    /// <summary>預設啟動參數。</summary>
    public string[] Args { get; init; } = [];

    /// <summary>此工具是否需要系統管理員權限（安裝前向使用者預告）。</summary>
    public bool RequiresAdmin { get; init; }
}
