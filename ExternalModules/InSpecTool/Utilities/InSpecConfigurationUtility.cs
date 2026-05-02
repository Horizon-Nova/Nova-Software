using ExternalModules.InSpecTool.Core;
using HorizonNova.HelixStudio.ModuleSdk;

namespace ExternalModules.InSpecTool.Utilities;

public static class InSpecConfigurationUtility
{
    private const string SetupKeyword = "SETUP";

    public static InSpecToolConfiguration QueryConfiguration(IShellContext? context)
    {
        var s = context?.Settings;

        return new InSpecToolConfiguration
        {
            BarcodeDebounceMs    = s?.QueryInt32  ("scanIntervalMs",       80)           ?? 80,
            EnableAutoMonitoring = s?.QueryBoolean("enableAutoMonitoring", true)         ?? true,
            MonitorIntervalMs    = s?.QueryInt32  ("monitorIntervalMs",    500)           ?? 500,
            EnableAutoRun        = s?.QueryBoolean("enableAutoRun",        true)         ?? true,
            SearchDirectory      = s?.QueryString ("searchDirectory",      string.Empty) ?? string.Empty,
            SystemLogDirectory   = s?.QueryString ("systemLogDirectory",   string.Empty) ?? string.Empty,
            BackupSavePath       = s?.QueryString ("backupSavePath",       string.Empty) ?? string.Empty,
            SetupKeyword         = SetupKeyword,
            FieldDefinitions     = InSpecFieldSchemaUtility.QueryDefinitions(
                                       s?.QueryString("fieldSchema", string.Empty) ?? string.Empty)
        };
    }
}
