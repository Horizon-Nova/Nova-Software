namespace Helix_Studio.Models;

/// <summary>workspaces.json 內容：所有工作區清單與目前選擇的工作區 Id。</summary>
public sealed class WorkspacesFile
{
    public List<WorkspaceInfo> Workspaces { get; set; } = [];
    public string CurrentId { get; set; } = string.Empty;
}
