namespace ExternalModules.InSpecTool.Core;

public sealed class PreviewResultItem
{
    public string FileName { get; init; } = string.Empty;

    public string PartNumber { get; init; } = string.Empty;

    public string Revision { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public bool HasPartNumber => !string.IsNullOrWhiteSpace(PartNumber);

    public bool HasRevision => !string.IsNullOrWhiteSpace(Revision);
}
