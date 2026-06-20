namespace Helix_Studio.Models;

/// <summary>
/// installed.json 中的一筆已安裝紀錄（持久化用，不含執行期狀態與 manifest 內容）。
/// </summary>
public sealed class InstalledToolRecord
{
    public string ToolId { get; init; } = string.Empty;
    public DateTime InstalledAt { get; init; }
    public ToolSettings Settings { get; init; } = new();
}
