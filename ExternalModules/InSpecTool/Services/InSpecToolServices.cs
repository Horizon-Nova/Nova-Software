using System.IO;
using ExternalModules.InSpecTool.Domain;
using ExternalModules.InSpecTool.Infrastructure;
using ExternalModules.InSpecTool.Model;
using HorizonNova.HelixStudio.ModuleSdk;
using Microsoft.Extensions.Logging;

namespace ExternalModules.InSpecTool.Services;

/// <summary>
/// 以薄 Facade 方式串接 ViewModel、Domain、Infrastructure 與宿主設定。
/// </summary>
internal sealed class InSpecToolServices : IDisposable
{
    private const string SetupKeyword = "SETUP";

    private readonly InSpecToolDomain _domain = new();
    private readonly InSpecToolInfrastructure _infrastructure = new();
    private IShellContext? _shellContext;

    /// <summary>
    /// 當自動監控偵測到需要輸入的欄位時引發。
    /// </summary>
    public event EventHandler<string>? AutoInputRequested
    {
        add => _infrastructure.AutoInputRequested += value;
        remove => _infrastructure.AutoInputRequested -= value;
    }

    /// <summary>
    /// 套用目前模組上下文。
    /// </summary>
    /// <param name="shellContext">宿主模組上下文。</param>
    public void ApplyShellContext(IShellContext? shellContext)
    {
        _shellContext = shellContext;
        _infrastructure.ApplyLogger(shellContext is null ? null : shellContext.Log);
    }

    /// <summary>
    /// 查詢目前組態快照。
    /// </summary>
    /// <returns>目前組態模型。</returns>
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

    /// <summary>
    /// 查詢條碼解析結果。
    /// </summary>
    /// <param name="barcode">條碼內容。</param>
    /// <param name="fieldDefinitions">欄位定義集合。</param>
    /// <returns>條碼解析結果模型。</returns>
    public InSpecBarcodeParseResultModel QueryBarcodeParseResult(string barcode, IReadOnlyList<InSpecFieldDefinitionModel> fieldDefinitions)
        => _domain.QueryBarcodeParseResult(barcode, fieldDefinitions);

    /// <summary>
    /// 驗證條碼解析結果。
    /// </summary>
    /// <param name="parseResult">條碼解析結果。</param>
    /// <returns>空字串表示可用，否則為錯誤訊息。</returns>
    public string ValidateBarcodeParseResult(InSpecBarcodeParseResultModel parseResult)
        => _domain.ValidateBarcodeParseResult(parseResult);

    /// <summary>
    /// 建立主頁欄位顯示模型集合。
    /// </summary>
    /// <param name="parseResult">條碼解析結果。</param>
    /// <param name="fieldDefinitions">欄位定義集合。</param>
    /// <returns>欄位顯示模型集合。</returns>
    public IReadOnlyList<InSpecPreviewFieldModel> QueryPreviewFieldModels(
        InSpecBarcodeParseResultModel parseResult,
        IReadOnlyList<InSpecFieldDefinitionModel> fieldDefinitions)
        => _domain.QueryPreviewFieldModels(parseResult, fieldDefinitions);

    /// <summary>
    /// 由欄位顯示模型集合解析條碼文字。
    /// </summary>
    /// <param name="fieldModels">欄位顯示模型集合。</param>
    /// <param name="fieldDefinitions">欄位定義集合。</param>
    /// <returns>組合後的條碼文字。</returns>
    public string ResolveBarcodeText(IEnumerable<InSpecPreviewFieldModel> fieldModels, IReadOnlyList<InSpecFieldDefinitionModel> fieldDefinitions)
        => _domain.ResolveBarcodeText(fieldModels, fieldDefinitions);

    /// <summary>
    /// 查詢檔案列表標題文字。
    /// </summary>
    /// <param name="isSearching">是否正在搜尋。</param>
    /// <returns>顯示用標題文字。</returns>
    public string ResolveFileListTitleText(bool isSearching) => _domain.ResolveFileListTitleText(isSearching);

    /// <summary>
    /// 查詢自動監控狀態文字。
    /// </summary>
    /// <param name="configuration">目前組態。</param>
    /// <returns>顯示用狀態文字。</returns>
    public string ResolveAutomationStatusText(InSpecToolConfigurationModel configuration)
        => _domain.ResolveAutomationStatusText(configuration, _shellContext is not null);

    /// <summary>
    /// 查詢符合條件的結果檔案集合。
    /// </summary>
    /// <param name="fieldModels">欄位顯示模型集合。</param>
    /// <param name="configuration">目前組態。</param>
    /// <returns>結果檔案集合與提示文字。</returns>
    public (IReadOnlyList<InSpecPreviewResultModel> Results, string PlaceholderText) QueryResults(
        IReadOnlyList<InSpecPreviewFieldModel> fieldModels,
        InSpecToolConfigurationModel configuration)
    {
        var partNumber = _domain.ResolveSearchPartNumber(fieldModels);
        if (string.IsNullOrWhiteSpace(partNumber))
        {
            return ([], "請先輸入或掃描條碼。");
        }

        if (string.IsNullOrWhiteSpace(configuration.SearchDirectory))
        {
            return ([], "請先在模組設定中指定搜尋資料夾。");
        }

        var normalizedPartNumber = _domain.ResolveNormalizedPartNumberForQuery(partNumber);
        try
        {
            var results = _infrastructure.QueryFiles(configuration.SearchDirectory, partNumber, normalizedPartNumber, configuration.SetupKeyword);
            return results.Count == 0 ? ([], "找不到符合條件的 InSpec 檔案。") : (results, string.Empty);
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

    /// <summary>
    /// 刪除檔案索引快取。
    /// </summary>
    public void DeleteFileQueryIndex() => _infrastructure.DeleteFileQueryIndex();

    /// <summary>
    /// 解析目前橋接輸出目標路徑。
    /// </summary>
    /// <param name="fieldModels">欄位顯示模型集合。</param>
    /// <param name="configuration">目前組態。</param>
    /// <returns>完整目標路徑。</returns>
    public string? ResolveCurrentTargetPath(IEnumerable<InSpecPreviewFieldModel> fieldModels, InSpecToolConfigurationModel configuration)
        => _domain.ResolveCurrentTargetPath(fieldModels, configuration);

    /// <summary>
    /// 驗證目前是否允許開啟檔案。
    /// </summary>
    /// <param name="fieldModels">欄位顯示模型集合。</param>
    /// <param name="message">驗證失敗訊息。</param>
    /// <returns>若允許開啟則回傳 <c>true</c>。</returns>
    public bool ValidateCanOpenFile(IEnumerable<InSpecPreviewFieldModel> fieldModels, out string message)
        => _domain.ValidateCanOpenFile(fieldModels, out message);

    /// <summary>
    /// 開啟結果檔案。
    /// </summary>
    /// <param name="resultModel">檔案結果模型。</param>
    /// <param name="configuration">目前組態。</param>
    public void OpenFile(InSpecPreviewResultModel resultModel, InSpecToolConfigurationModel configuration)
        => _infrastructure.OpenFile(resultModel, configuration.EnableAutoRun);

    /// <summary>
    /// 套用自動監控組態。
    /// </summary>
    /// <param name="configuration">目前組態。</param>
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

    /// <summary>
    /// 啟用檔案橋接。
    /// </summary>
    public void ActivateFileBridge() => _infrastructure.ActivateFileBridge();

    /// <summary>
    /// 停用檔案橋接。
    /// </summary>
    public void DeactivateFileBridge() => _infrastructure.DeactivateFileBridge();

    /// <summary>
    /// 更新目前橋接輸出目標路徑。
    /// </summary>
    /// <param name="targetPath">目標路徑。</param>
    public void UpdateCurrentTargetPath(string? targetPath) => _infrastructure.UpdateCurrentTargetPath(targetPath);

    /// <summary>
    /// 套用自動輸入內容。
    /// </summary>
    /// <param name="value">欲輸入的內容。</param>
    /// <returns>若輸入成功則回傳 <c>true</c>。</returns>
    public bool ValidateAutoInput(string value) => _infrastructure.ValidateAutoInput(value);

    /// <summary>
    /// 查詢欄位名稱對應的定義模型。
    /// </summary>
    /// <param name="fieldDefinitions">欄位定義集合。</param>
    /// <param name="fieldName">欄位名稱。</param>
    /// <returns>符合的欄位定義模型。</returns>
    public InSpecFieldDefinitionModel? QueryFieldDefinition(IEnumerable<InSpecFieldDefinitionModel> fieldDefinitions, string fieldName)
        => fieldDefinitions.FirstOrDefault(fieldDefinition => string.Equals(fieldDefinition.Name, fieldName, StringComparison.Ordinal));

    /// <summary>
    /// 驗證欄位顯示模型是否需要提示確認。
    /// </summary>
    /// <param name="fieldModel">欄位顯示模型。</param>
    /// <returns>若需要提示確認則回傳 <c>true</c>。</returns>
    public bool ValidateRequiresConfirmation(InSpecPreviewFieldModel fieldModel) => _domain.ValidateRequiresConfirmation(fieldModel);

    /// <summary>
    /// 驗證欄位定義是否需要提示確認。
    /// </summary>
    /// <param name="fieldDefinition">欄位定義模型。</param>
    /// <returns>若需要提示確認則回傳 <c>true</c>。</returns>
    public bool ValidateRequiresConfirmation(InSpecFieldDefinitionModel fieldDefinition) => _domain.ValidateRequiresConfirmation(fieldDefinition);

    /// <summary>
    /// 查詢設定頁固定選項集合。
    /// </summary>
    /// <returns>設定頁選項模型。</returns>
    public InSpecSettingsOptionModel QuerySettingsOptionModel() => _domain.QuerySettingsOptionModel();

    /// <summary>
    /// 查詢設定頁資料快照。
    /// </summary>
    /// <returns>設定頁資料快照模型。</returns>
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

    /// <summary>
    /// 建立新的設定頁欄位結構列。
    /// </summary>
    /// <returns>新的欄位結構列模型。</returns>
    public InSpecSettingsFieldRowModel CreateSettingsFieldRow() => _domain.CreateSettingsFieldRow();

    /// <summary>
    /// 儲存字串型設定。
    /// </summary>
    /// <param name="key">設定鍵名。</param>
    /// <param name="value">設定值。</param>
    /// <returns>儲存結果模型。</returns>
    public InSpecSettingSaveResultModel SaveStringSetting(string key, string? value)
        => ResolveSettingSaveResult(_shellContext?.Settings.SaveString(key, value));

    /// <summary>
    /// 儲存布林型設定。
    /// </summary>
    /// <param name="key">設定鍵名。</param>
    /// <param name="value">設定值。</param>
    /// <returns>儲存結果模型。</returns>
    public InSpecSettingSaveResultModel SaveBooleanSetting(string key, bool value)
        => ResolveSettingSaveResult(_shellContext?.Settings.SaveBoolean(key, value));

    /// <summary>
    /// 序列化欄位結構設定內容。
    /// </summary>
    /// <param name="fieldRows">欄位結構列集合。</param>
    /// <returns>可儲存的 JSON 字串。</returns>
    public string ResolveFieldSchemaText(IEnumerable<InSpecSettingsFieldRowModel> fieldRows) => _domain.ResolveFieldSchemaText(fieldRows);

    /// <summary>
    /// 釋放服務層資源。
    /// </summary>
    public void Dispose() => _infrastructure.Dispose();

    private string QuerySavedString(string key, string fallback)
    {
        var value = _shellContext?.Settings.QueryString(key, fallback) ?? fallback;
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private InSpecSettingSaveResultModel ResolveSettingSaveResult(ModuleSettingSaveResult? result)
    {
        if (result is null)
        {
            return new InSpecSettingSaveResultModel
            {
                IsSuccess = false,
                DetailText = "模組上下文尚未建立。"
            };
        }

        return new InSpecSettingSaveResultModel
        {
            IsSuccess = result.IsSuccess,
            DetailText = result.DetailText
        };
    }
}
