namespace Helix_Studio.Models;

public sealed class ToolSettings
{
    public string[] Args { get; set; } = [];
    public bool AutoStart { get; set; } = false;
    public int SortIndex { get; set; } = int.MaxValue;
}
