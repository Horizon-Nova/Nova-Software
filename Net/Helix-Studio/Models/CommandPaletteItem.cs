namespace Helix_Studio.Models;

public sealed class CommandPaletteItem
{
    public string Title    { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string IconPath { get; init; } = string.Empty;
    public string NavKey   { get; init; } = string.Empty;
    public string Section  { get; init; } = string.Empty;
}
