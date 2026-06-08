namespace ExternalModules.InSpecTool.Model;

internal sealed class InSpecBarcodeParseResultModel
{
    public string RawBarcode { get; init; } = string.Empty;

    public string CleanedBarcode { get; init; } = string.Empty;

    public IReadOnlyList<string> Parts { get; init; } = [];

    public IReadOnlyList<string> FieldValues { get; init; } = [];

    public bool HasInput => !string.IsNullOrWhiteSpace(RawBarcode);

    public bool HasBlockedCharacter { get; init; }

    public bool HasLeadingSeparator { get; init; }
}

internal sealed class InSpecFieldDefinitionModel
{
    public const string DirectInputTargetMode = "Direct Input";

    public const string PromptConfirmationTargetMode = "Prompt Confirmation";

    public string Name { get; init; } = string.Empty;

    public string Keyword { get; init; } = string.Empty;

    public bool IsEditable { get; init; } = true;

    public string TargetMode { get; init; } = DirectInputTargetMode;

    public bool IsLocked { get; init; }
}

internal sealed class InSpecToolConfigurationModel
{
    public int BarcodeDebounceMs { get; init; } = 80;

    public bool EnableAutoMonitoring { get; init; } = true;

    public int MonitorIntervalMs { get; init; } = 500;

    public bool EnableAutoRun { get; init; } = true;

    public string SearchDirectory { get; init; } = string.Empty;

    public string SystemLogDirectory { get; init; } = string.Empty;

    public string BackupSavePath { get; init; } = string.Empty;

    public string SetupKeyword { get; init; } = "SETUP";

    public IReadOnlyList<InSpecFieldDefinitionModel> FieldDefinitions { get; init; } = [];
}

internal sealed class InSpecPreviewFieldModel
{
    public string Label { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public bool IsReadOnly { get; init; }

    public string Keyword { get; init; } = string.Empty;

    public string TargetMode { get; init; } = InSpecFieldDefinitionModel.DirectInputTargetMode;
}

internal sealed class InSpecPreviewResultModel
{
    public string FileName { get; init; } = string.Empty;

    public string PartNumber { get; init; } = string.Empty;

    public string Revision { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;
}

internal sealed class InSpecSettingsFieldRowModel
{
    public string Name { get; init; } = string.Empty;

    public string Keyword { get; init; } = string.Empty;

    public string TargetMode { get; init; } = InSpecFieldDefinitionModel.DirectInputTargetMode;

    public bool IsEditable { get; init; } = true;

    public bool IsLocked { get; init; }
}

internal sealed class InSpecSettingsOptionModel
{
    public IReadOnlyList<string> ScanIntervalOptions { get; init; } = [];

    public IReadOnlyList<string> MonitorIntervalOptions { get; init; } = [];

    public IReadOnlyList<string> TargetModeOptions { get; init; } = [];
}

internal sealed class InSpecSettingsSnapshotModel
{
    public string ScanIntervalMs { get; init; } = "80";

    public string MonitorIntervalMs { get; init; } = "500";

    public bool EnableAutoMonitoring { get; init; } = true;

    public bool EnableAutoRun { get; init; } = true;

    public string SearchDirectory { get; init; } = string.Empty;

    public string SystemLogDirectory { get; init; } = string.Empty;

    public string BackupSavePath { get; init; } = string.Empty;

    public IReadOnlyList<InSpecSettingsFieldRowModel> FieldRows { get; init; } = [];
}

internal sealed class InSpecSettingSaveResultModel
{
    public bool IsSuccess { get; init; }

    public string DetailText { get; init; } = string.Empty;
}
