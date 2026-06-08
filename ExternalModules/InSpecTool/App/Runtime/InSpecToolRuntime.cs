using System.IO;
using ExternalModules.InSpecTool.Domain;
using ExternalModules.InSpecTool.Infrastructure;
using ExternalModules.InSpecTool.Model;
using HorizonNova.HelixStudio.ModuleSdk;
using Microsoft.Extensions.Logging;

namespace ExternalModules.InSpecTool.App.Runtime;

internal sealed class InSpecToolRuntime : IDisposable
{
    private const string SetupKeyword = "SETUP";

    private readonly InSpecToolDomain _domain = new();
    private readonly InSpecToolInfrastructure _infrastructure = new();
    private IShellContext? _shellContext;

    public event EventHandler<string>? AutoInputRequested
    {
        add => _infrastructure.AutoInputRequested += value;
        remove => _infrastructure.AutoInputRequested -= value;
    }

    public void ApplyShellContext(IShellContext? shellContext)
    {
        _shellContext = shellContext;
        _infrastructure.ApplyLogger(shellContext?.Log);
    }

    public InSpecToolConfigurationModel QueryConfiguration()
    {
        var settings = _shellContext?.Settings;
        return new InSpecToolConfigurationModel
        {
            BarcodeDebounceMs = settings?.QueryInt32("scanIntervalMs", 80) ?? 80,
            EnableAutoMonitoring = settings?.QueryBoolean("enableAutoMonitoring", true) ?? true,
            MonitorIntervalMs = settings?.QueryInt32("monitorIntervalMs", 500) ?? 500,
            EnableAutoRun = settings?.QueryBoolean("enableAutoRun", true) ?? true,
            SearchDirectory = settings?.QueryString("searchDirectory", string.Empty) ?? string.Empty,
            SystemLogDirectory = settings?.QueryString("systemLogDirectory", string.Empty) ?? string.Empty,
            BackupSavePath = settings?.QueryString("backupSavePath", string.Empty) ?? string.Empty,
            SetupKeyword = SetupKeyword,
            FieldDefinitions = _domain.QueryFieldDefinitions(settings?.QueryString("fieldSchema", string.Empty) ?? string.Empty)
        };
    }

    public string QueryModuleLicenseKey()
        => _shellContext?.Auth.QueryLicenseKey() ?? string.Empty;

    public ModuleSettingSaveResult SaveModuleLicenseKey(string? licenseKey)
        => _shellContext?.Auth.SaveLicenseKey(licenseKey) ?? new ModuleSettingSaveResult
        {
            IsSuccess = false,
            DetailText = "目前沒有可用的模組授權儲存環境。"
        };

    public ModuleLicenseVerificationResult VerifyModuleLicenseKey(string? licenseKey)
        => _shellContext?.VerifyModuleLicense(licenseKey) ?? new ModuleLicenseVerificationResult
        {
            IsValid = false,
            ErrorMessage = "目前沒有可用的模組授權驗證環境。"
        };

    public bool ValidateStoredModuleLicense()
    {
        if (_shellContext is null)
        {
            return false;
        }

        var storedKey = QueryModuleLicenseKey();
        return _shellContext.VerifyModuleLicense(storedKey).IsValid;
    }

    public InSpecBarcodeParseResultModel QueryBarcodeParseResult(string barcode, IReadOnlyList<InSpecFieldDefinitionModel> fieldDefinitions)
        => _domain.QueryBarcodeParseResult(barcode, fieldDefinitions);

    public string ValidateBarcodeParseResult(InSpecBarcodeParseResultModel parseResult)
        => _domain.ValidateBarcodeParseResult(parseResult);

    public IReadOnlyList<InSpecPreviewFieldModel> QueryPreviewFieldModels(
        InSpecBarcodeParseResultModel parseResult,
        IReadOnlyList<InSpecFieldDefinitionModel> fieldDefinitions)
        => _domain.QueryPreviewFieldModels(parseResult, fieldDefinitions);

    public string ResolveBarcodeText(IEnumerable<InSpecPreviewFieldModel> fieldModels, IReadOnlyList<InSpecFieldDefinitionModel> fieldDefinitions)
        => _domain.ResolveBarcodeText(fieldModels, fieldDefinitions);

    public string ResolveFileListTitleText(bool isSearching)
        => _domain.ResolveFileListTitleText(isSearching);

    public string ResolveAutomationStatusText(InSpecToolConfigurationModel configuration)
        => _domain.ResolveAutomationStatusText(configuration, _shellContext is not null);

    public (IReadOnlyList<InSpecPreviewResultModel> Results, string PlaceholderText) QueryResults(
        IReadOnlyList<InSpecPreviewFieldModel> fieldModels,
        InSpecToolConfigurationModel configuration)
    {
        var partNumber = _domain.ResolveSearchPartNumber(fieldModels);
        if (string.IsNullOrWhiteSpace(partNumber))
        {
            return ([], "請先輸入或解析條碼內容。");
        }

        if (string.IsNullOrWhiteSpace(configuration.SearchDirectory))
        {
            return ([], "請先在設定中指定搜尋資料夾。");
        }

        var normalizedPartNumber = _domain.ResolveNormalizedPartNumberForQuery(partNumber);
        try
        {
            var results = _infrastructure.QueryFiles(configuration.SearchDirectory, partNumber, normalizedPartNumber, configuration.SetupKeyword);
            return results.Count == 0
                ? ([], "查無符合條件的 InSpec 檔案。")
                : (results, string.Empty);
        }
        catch (DirectoryNotFoundException exception)
        {
            return ([], exception.Message);
        }
        catch (Exception exception)
        {
            return ([], exception.Message);
        }
    }

    public void DeleteFileQueryIndex()
        => _infrastructure.DeleteFileQueryIndex();

    public string? ResolveCurrentTargetPath(IEnumerable<InSpecPreviewFieldModel> fieldModels, InSpecToolConfigurationModel configuration)
        => _domain.ResolveCurrentTargetPath(fieldModels, configuration);

    public bool ValidateCanOpenFile(IEnumerable<InSpecPreviewFieldModel> fieldModels, out string message)
        => _domain.ValidateCanOpenFile(fieldModels, out message);

    public void OpenFile(InSpecPreviewResultModel resultModel, InSpecToolConfigurationModel configuration)
        => _infrastructure.OpenFile(resultModel, configuration.EnableAutoRun);

    public void ApplyAutomationConfiguration(InSpecToolConfigurationModel configuration)
    {
        _infrastructure.ApplyAutomationConfiguration(configuration);
        if (configuration.EnableAutoMonitoring && _shellContext is not null)
        {
            _infrastructure.ActivateAutomationMonitoring();
            return;
        }

        _infrastructure.DeactivateAutomationMonitoring();
    }

    public void ActivateFileBridge()
        => _infrastructure.ActivateFileBridge();

    public void DeactivateFileBridge()
        => _infrastructure.DeactivateFileBridge();

    public void UpdateCurrentTargetPath(string? targetPath)
        => _infrastructure.UpdateCurrentTargetPath(targetPath);

    public bool ValidateAutoInput(string value)
        => _infrastructure.ValidateAutoInput(value);

    public InSpecFieldDefinitionModel? QueryFieldDefinition(IEnumerable<InSpecFieldDefinitionModel> fieldDefinitions, string fieldName)
        => fieldDefinitions.FirstOrDefault(fieldDefinition => string.Equals(fieldDefinition.Name, fieldName, StringComparison.Ordinal));

    public bool ValidateRequiresConfirmation(InSpecPreviewFieldModel fieldModel)
        => _domain.ValidateRequiresConfirmation(fieldModel);

    public bool ValidateRequiresConfirmation(InSpecFieldDefinitionModel fieldDefinition)
        => _domain.ValidateRequiresConfirmation(fieldDefinition);

    public InSpecSettingsOptionModel QuerySettingsOptionModel()
        => _domain.QuerySettingsOptionModel();

    public InSpecSettingsSnapshotModel QuerySettingsSnapshot()
    {
        var configuration = QueryConfiguration();
        return new InSpecSettingsSnapshotModel
        {
            ScanIntervalMs = QuerySavedString("scanIntervalMs", "80"),
            MonitorIntervalMs = QuerySavedString("monitorIntervalMs", "500"),
            EnableAutoMonitoring = configuration.EnableAutoMonitoring,
            EnableAutoRun = configuration.EnableAutoRun,
            SearchDirectory = configuration.SearchDirectory,
            SystemLogDirectory = configuration.SystemLogDirectory,
            BackupSavePath = configuration.BackupSavePath,
            FieldRows = _domain.QuerySettingsFieldRows(configuration.FieldDefinitions)
        };
    }

    public InSpecSettingsFieldRowModel CreateSettingsFieldRow()
        => _domain.CreateSettingsFieldRow();

    public InSpecSettingSaveResultModel SaveStringSetting(string key, string? value)
        => ResolveSettingSaveResult(_shellContext?.Settings.SaveString(key, value));

    public InSpecSettingSaveResultModel SaveBooleanSetting(string key, bool value)
        => ResolveSettingSaveResult(_shellContext?.Settings.SaveBoolean(key, value));

    public string ResolveFieldSchemaText(IEnumerable<InSpecSettingsFieldRowModel> fieldRows)
        => _domain.ResolveFieldSchemaText(fieldRows);

    public void Dispose()
        => _infrastructure.Dispose();

    private string QuerySavedString(string key, string fallback)
    {
        var value = _shellContext?.Settings.QueryString(key, fallback) ?? fallback;
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static InSpecSettingSaveResultModel ResolveSettingSaveResult(ModuleSettingSaveResult? result)
    {
        if (result is null)
        {
            return new InSpecSettingSaveResultModel
            {
                IsSuccess = false,
                DetailText = "目前沒有可用的模組設定儲存環境。"
            };
        }

        return new InSpecSettingSaveResultModel
        {
            IsSuccess = result.IsSuccess,
            DetailText = result.DetailText
        };
    }
}
