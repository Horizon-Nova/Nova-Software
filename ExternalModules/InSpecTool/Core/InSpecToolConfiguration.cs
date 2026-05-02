namespace ExternalModules.InSpecTool.Core;

public sealed class InSpecToolConfiguration
{
    public int BarcodeDebounceMs { get; init; } = 80;

    public bool EnableAutoMonitoring { get; init; } = true;

    public int MonitorIntervalMs { get; init; } = 500;

    public bool EnableAutoRun { get; init; } = true;

    public string SearchDirectory { get; init; } = string.Empty;

    public string SystemLogDirectory { get; init; } = string.Empty;

    public string BackupSavePath { get; init; } = string.Empty;

    public string SetupKeyword { get; init; } = "SETUP";

    public IReadOnlyList<InSpecFieldDefinition> FieldDefinitions { get; init; } = [];
}
