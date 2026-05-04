using ExternalModules.InSpecTool.Core;
using HN_Studio.ModuleSdk;

namespace ExternalModules.InSpecTool.Utilities;

public static class InSpecConfigurationUtility
{
    private const string SetupKeyword = "SETUP";

    public static InSpecToolConfiguration QueryConfiguration(HnModuleContext? context)
    {
        var settings = context?.Settings ?? HnEmptyModuleSettings.Instance;

        return new InSpecToolConfiguration
        {
            BarcodeDebounceMs = settings.QueryInt32("scanIntervalMs", 80),
            EnableAutoMonitoring = settings.QueryBoolean("enableAutoMonitoring", true),
            MonitorIntervalMs = settings.QueryInt32("monitorIntervalMs", 500),
            EnableAutoRun = settings.QueryBoolean("enableAutoRun", true),
            SearchDirectory = settings.QueryString("searchDirectory", string.Empty),
            SystemLogDirectory = settings.QueryString("systemLogDirectory", string.Empty),
            BackupSavePath = settings.QueryString("backupSavePath", string.Empty),
            SetupKeyword = SetupKeyword,
            FieldDefinitions = InSpecFieldSchemaUtility.QueryDefinitions(settings.QueryString("fieldSchema", string.Empty))
        };
    }
}
