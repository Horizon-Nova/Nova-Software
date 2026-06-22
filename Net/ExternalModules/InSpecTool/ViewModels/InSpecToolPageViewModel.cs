using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using ExternalModules.InSpecTool.App.Runtime;
using ExternalModules.InSpecTool.Model;
using ExternalModules.InSpecTool.ViewModels.Base;

namespace ExternalModules.InSpecTool.ViewModels;

/// <summary>
/// InSpecTool 主頁的 UI 狀態機。
/// </summary>
internal sealed class InSpecToolPageViewModel : ViewModelBase, IDisposable
{
    private readonly InSpecToolRuntime _services;
    private CancellationTokenSource? _barcodeRefreshCancellation;
    private InSpecToolConfigurationModel _configuration = new();
    private string _barcodeText = string.Empty;
    private string _automationStatusText = "自動監控：待模組啟用";
    private string _fileListTitleText = "檔案結果";
    private string _fileListPlaceholderText = string.Empty;
    private string _licenseStatusText = "模組授權驗證中。";
    private bool _isBusy;
    private bool _isDirty;
    private bool _isLicenseValid;
    private bool _isPromptingForLicense;
    private InSpecPreviewResultModel? _selectedResultModel;
    private int _searchVersion;

    /// <summary>
    /// 建立主頁 ViewModel。
    /// </summary>
    /// <param name="services">服務層實例。</param>
    public InSpecToolPageViewModel(InSpecToolRuntime services)
    {
        _services = services;
        FieldModels = [];
        ResultModels = [];
        ApplyClearBarcodeCommand = new RelayCommand(ApplyClearBarcode);
        ApplyRefreshQueryCommand = new RelayCommand(ApplyRefreshQuery);
        OpenSelectedFileCommand = new RelayCommand(ApplyOpenSelectedFile, ValidateCanOpenSelectedFile);
        _services.AutoInputRequested += ApplyAutoInputRequested;
        RefreshLicenseState(showSuccessMessage: false);
    }

    /// <summary>
    /// 主頁顯示的欄位模型集合。
    /// </summary>
    public ObservableCollection<InSpecPreviewFieldItemViewModel> FieldModels { get; }

    /// <summary>
    /// 主頁顯示的檔案結果集合。
    /// </summary>
    public ObservableCollection<InSpecPreviewResultModel> ResultModels { get; }

    /// <summary>
    /// 條碼輸入內容。
    /// </summary>
    public string BarcodeText
    {
        get => _barcodeText;
        set
        {
            if (!SetProperty(ref _barcodeText, value ?? string.Empty))
            {
                return;
            }

            IsDirty = true;
            ApplyBarcodeRefreshDebounce();
        }
    }

    /// <summary>
    /// 自動監控狀態文字。
    /// </summary>
    public string AutomationStatusText
    {
        get => _automationStatusText;
        private set => SetProperty(ref _automationStatusText, value);
    }

    /// <summary>
    /// 檔案列表標題文字。
    /// </summary>
    public string FileListTitleText
    {
        get => _fileListTitleText;
        private set => SetProperty(ref _fileListTitleText, value);
    }

    /// <summary>
    /// 檔案列表提示文字。
    /// </summary>
    public string FileListPlaceholderText
    {
        get => _fileListPlaceholderText;
        private set
        {
            if (!SetProperty(ref _fileListPlaceholderText, value ?? string.Empty))
            {
                return;
            }

            RaisePropertyChanged(nameof(IsFileListPlaceholderVisible));
        }
    }

    /// <summary>
    /// 是否顯示檔案列表提示文字。
    /// </summary>
    public bool IsFileListPlaceholderVisible => !string.IsNullOrWhiteSpace(FileListPlaceholderText) && ResultModels.Count == 0;

    /// <summary>
    /// 是否正在執行查詢。
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
            {
                return;
            }

            RelayCommand.ApplyInvalidate();
        }
    }

    /// <summary>
    /// 使用者是否已修改欄位內容。
    /// </summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    /// <summary>
    /// 模組授權狀態文字。
    /// </summary>
    public string LicenseStatusText
    {
        get => _licenseStatusText;
        private set => SetProperty(ref _licenseStatusText, value);
    }

    /// <summary>
    /// 目前模組授權是否有效。
    /// </summary>
    public bool IsLicenseValid
    {
        get => _isLicenseValid;
        private set
        {
            if (!SetProperty(ref _isLicenseValid, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(IsLicenseMissing));
            RaisePropertyChanged(nameof(IsMainContentEnabled));
        }
    }

    /// <summary>
    /// 目前模組授權是否缺失。
    /// </summary>
    public bool IsLicenseMissing => !IsLicenseValid;

    /// <summary>
    /// 主內容是否可用。
    /// </summary>
    public bool IsMainContentEnabled => IsLicenseValid;

    /// <summary>
    /// 目前選取的檔案結果。
    /// </summary>
    public InSpecPreviewResultModel? SelectedResultModel
    {
        get => _selectedResultModel;
        set
        {
            if (!SetProperty(ref _selectedResultModel, value))
            {
                return;
            }

            RelayCommand.ApplyInvalidate();
        }
    }

    /// <summary>
    /// 清除條碼輸入內容的命令。
    /// </summary>
    public ICommand ApplyClearBarcodeCommand { get; }

    /// <summary>
    /// 重新查詢檔案結果的命令。
    /// </summary>
    public ICommand ApplyRefreshQueryCommand { get; }

    /// <summary>
    /// 開啟目前選取檔案的命令。
    /// </summary>
    public ICommand OpenSelectedFileCommand { get; }

    /// <summary>
    /// 要求 View 顯示提示輸入對話框時引發。
    /// </summary>
    public event EventHandler<InSpecValuePromptRequestModel>? ValuePromptRequested;

    /// <summary>
    /// 要求 View 顯示授權輸入對話框時引發。
    /// </summary>
    public event EventHandler<InSpecLicensePromptRequestModel>? LicensePromptRequested;

    /// <summary>
    /// 要求 View 顯示訊息對話框時引發。
    /// </summary>
    public event EventHandler<InSpecMessageRequestModel>? MessageRequested;

    /// <summary>
    /// 套用目前模組上下文。
    /// </summary>
    internal void ApplyShellContext()
    {
        _configuration = _services.QueryConfiguration();
        _services.ActivateFileBridge();
        _services.ApplyAutomationConfiguration(_configuration);
        RefreshLicenseState(showSuccessMessage: false);
        AutomationStatusText = _services.ResolveAutomationStatusText(_configuration);
        ApplyLicensePromptIfNeeded();
        _ = ApplyBarcodeRefreshAsync();
    }

    /// <summary>
    /// 清除目前模組上下文。
    /// </summary>
    internal void ClearShellContext()
    {
        _services.ApplyAutomationConfiguration(new InSpecToolConfigurationModel { EnableAutoMonitoring = false });
        _services.DeactivateFileBridge();
        ApplyLicenseLockedState();
        AutomationStatusText = _services.ResolveAutomationStatusText(_configuration);
    }

    /// <summary>
    /// 釋放主頁 ViewModel 使用的資源。
    /// </summary>
    public void Dispose()
    {
        _services.AutoInputRequested -= ApplyAutoInputRequested;
        foreach (var item in FieldModels)
        {
            item.PropertyChanged -= ApplyFieldItemValueChanged;
        }

        _barcodeRefreshCancellation?.Cancel();
        _barcodeRefreshCancellation?.Dispose();
    }

    private void RefreshLicenseState(bool showSuccessMessage)
    {
        var storedLicenseKey = _services.QueryModuleLicenseKey();
        var verification = _services.VerifyModuleLicenseKey(storedLicenseKey.Trim());
        IsLicenseValid = verification.IsValid;

        if (verification.IsValid)
        {
            LicenseStatusText = showSuccessMessage ? "模組授權已通過驗證。" : "模組授權有效。";
            FileListPlaceholderText = string.Empty;
            return;
        }

        LicenseStatusText = string.IsNullOrWhiteSpace(verification.ErrorMessage)
            ? "模組授權無效，請重新輸入金鑰。"
            : verification.ErrorMessage;
        ApplyLicenseLockedState();
    }

    private void ApplyLicenseLockedState()
    {
        IsLicenseValid = false;
        ResultModels.Clear();
        _services.UpdateCurrentTargetPath(null);
        FileListTitleText = "檔案結果";
        FileListPlaceholderText = "請先在模組主頁輸入有效的模組金鑰。";
        RaisePropertyChanged(nameof(IsFileListPlaceholderVisible));
    }

    private void ApplyLicensePromptIfNeeded()
    {
        if (IsLicenseValid || _isPromptingForLicense)
        {
            return;
        }

        _isPromptingForLicense = true;
        LicensePromptRequested?.Invoke(this, new InSpecLicensePromptRequestModel
        {
            StatusText = LicenseStatusText,
            ApplySubmittedLicenseKey = ApplySubmittedLicenseKey
        });
    }

    private void ApplySubmittedLicenseKey(string? licenseKey)
    {
        _isPromptingForLicense = false;
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            LicenseStatusText = "模組授權未通過，功能維持鎖定。";
            ApplyLicenseLockedState();
            return;
        }

        IsBusy = true;
        var saveResult = _services.SaveModuleLicenseKey(licenseKey.Trim());
        IsBusy = false;

        if (!saveResult.IsSuccess)
        {
            LicenseStatusText = $"模組授權儲存失敗：{saveResult.DetailText}";
            ApplyLicenseLockedState();
            return;
        }

        RefreshLicenseState(showSuccessMessage: true);
    }

    private void ApplyClearBarcode()
    {
        BarcodeText = string.Empty;
    }

    private async void ApplyRefreshQuery()
    {
        _services.DeleteFileQueryIndex();
        await ApplyBarcodeRefreshAsync();
    }

    private void ApplyBarcodeRefreshDebounce()
    {
        _barcodeRefreshCancellation?.Cancel();
        _barcodeRefreshCancellation?.Dispose();
        _barcodeRefreshCancellation = new CancellationTokenSource();
        var cancellationToken = _barcodeRefreshCancellation.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(Math.Max(50, _configuration.BarcodeDebounceMs), cancellationToken);
                await Application.Current.Dispatcher.InvokeAsync(ApplyBarcodeRefreshAsync);
            }
            catch (TaskCanceledException)
            {
            }
        }, cancellationToken);
    }

    private async Task ApplyBarcodeRefreshAsync()
    {
        var parseResult = _services.QueryBarcodeParseResult(BarcodeText, _configuration.FieldDefinitions);
        ApplyFieldModels(parseResult);
        await ApplyResultRefreshAsync(parseResult);
    }

    private async Task ApplyFieldRefreshAsync()
    {
        BarcodeText = _services.ResolveBarcodeText(FieldModels.Select(f => f.BuildModel()), _configuration.FieldDefinitions);
        await ApplyResultRefreshAsync(null);
    }

    private void ApplyFieldModels(InSpecBarcodeParseResultModel parseResult)
    {
        foreach (var item in FieldModels)
        {
            item.PropertyChanged -= ApplyFieldItemValueChanged;
        }

        FieldModels.Clear();
        foreach (var fieldModel in _services.QueryPreviewFieldModels(parseResult, _configuration.FieldDefinitions))
        {
            var itemVm = InSpecPreviewFieldItemViewModel.Create(fieldModel);
            itemVm.PropertyChanged += ApplyFieldItemValueChanged;
            FieldModels.Add(itemVm);
        }
    }

    private void ApplyFieldItemValueChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName != nameof(InSpecPreviewFieldItemViewModel.Value))
        {
            return;
        }

        var newBarcode = _services.ResolveBarcodeText(FieldModels.Select(f => f.BuildModel()), _configuration.FieldDefinitions);
        if (string.Equals(_barcodeText, newBarcode, StringComparison.Ordinal))
        {
            return;
        }

        _barcodeText = newBarcode;
        IsDirty = true;
        RaisePropertyChanged(nameof(BarcodeText));
        ApplyBarcodeRefreshDebounce();
    }

    private async Task ApplyResultRefreshAsync(InSpecBarcodeParseResultModel? parseResult)
    {
        var validationMessage = _services.ValidateBarcodeParseResult(parseResult ?? _services.QueryBarcodeParseResult(BarcodeText, _configuration.FieldDefinitions));
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            ResultModels.Clear();
            _services.UpdateCurrentTargetPath(null);
            FileListTitleText = _services.ResolveFileListTitleText(false);
            FileListPlaceholderText = validationMessage;
            RaisePropertyChanged(nameof(IsFileListPlaceholderVisible));
            return;
        }

        var searchVersion = ++_searchVersion;
        var fieldSnapshot = FieldModels.Select(f => f.BuildModel()).ToArray();

        IsBusy = true;
        FileListTitleText = _services.ResolveFileListTitleText(true);
        var queryResult = await Task.Run(() => _services.QueryResults(fieldSnapshot, _configuration));
        if (searchVersion != _searchVersion)
        {
            return;
        }

        ResultModels.Clear();
        foreach (var resultModel in queryResult.Results)
        {
            ResultModels.Add(resultModel);
        }

        _services.UpdateCurrentTargetPath(_services.ResolveCurrentTargetPath(fieldSnapshot, _configuration));
        FileListTitleText = _services.ResolveFileListTitleText(false);
        FileListPlaceholderText = queryResult.PlaceholderText;
        IsBusy = false;
        IsDirty = false;
        RaisePropertyChanged(nameof(IsFileListPlaceholderVisible));
    }

    private bool ValidateCanOpenSelectedFile(object? parameter)
        => parameter is InSpecPreviewResultModel || SelectedResultModel is not null;

    private void ApplyOpenSelectedFile(object? parameter)
    {
        var resultModel = parameter as InSpecPreviewResultModel ?? SelectedResultModel;
        if (resultModel is null)
        {
            return;
        }

        if (!_services.ValidateCanOpenFile(FieldModels.Select(f => f.BuildModel()), out var message))
        {
            MessageRequested?.Invoke(this, InSpecMessageRequestModel.CreateWarning("InSpec 工具", message));
            return;
        }

        try
        {
            _services.OpenFile(resultModel, _configuration);
        }
        catch (Exception exception)
        {
            MessageRequested?.Invoke(this, InSpecMessageRequestModel.CreateError("InSpec 工具", exception.Message));
        }
    }

    private void ApplyAutoInputRequested(object? sender, string inputTypeName)
    {
        Application.Current.Dispatcher.Invoke(() => ApplyAutoInputRequestedOnUiThread(inputTypeName));
    }

    private void ApplyAutoInputRequestedOnUiThread(string inputTypeName)
    {
        var fieldDefinition = _services.QueryFieldDefinition(_configuration.FieldDefinitions, inputTypeName);
        if (fieldDefinition is null)
        {
            return;
        }

        var fieldModel = FieldModels.FirstOrDefault(model => string.Equals(model.Label, inputTypeName, StringComparison.Ordinal));
        var currentValue = fieldModel?.Value?.Trim() ?? string.Empty;

        if (_services.ValidateRequiresConfirmation(fieldDefinition))
        {
            ValuePromptRequested?.Invoke(this, new InSpecValuePromptRequestModel
            {
                InputTypeName = inputTypeName,
                DefaultValue = currentValue,
                ApplyConfirmedValue = ApplyConfirmedAutoInput
            });
            return;
        }

        if (string.IsNullOrWhiteSpace(currentValue))
        {
            MessageRequested?.Invoke(this, InSpecMessageRequestModel.CreateInformation("InSpec 工具", $"請先在欄位「{inputTypeName}」填入內容。"));
            return;
        }

        ApplyConfirmedAutoInput(currentValue);
    }

    private void ApplyConfirmedAutoInput(string? confirmedValue)
    {
        if (string.IsNullOrWhiteSpace(confirmedValue))
        {
            return;
        }

        if (!_services.ValidateAutoInput(confirmedValue))
        {
            MessageRequested?.Invoke(this, InSpecMessageRequestModel.CreateWarning("InSpec 工具", "自動輸入失敗，請確認 InSpec 對話框仍在前景且可操作。"));
        }
    }
}

/// <summary>
/// 提示輸入對話框請求模型。
/// </summary>
internal sealed class InSpecValuePromptRequestModel
{
    /// <summary>
    /// 欄位名稱。
    /// </summary>
    public string InputTypeName { get; init; } = string.Empty;

    /// <summary>
    /// 預設輸入值。
    /// </summary>
    public string DefaultValue { get; init; } = string.Empty;

    /// <summary>
    /// 對話框確認後要套用的回呼。
    /// </summary>
    public Action<string?> ApplyConfirmedValue { get; init; } = _ => { };
}

/// <summary>
/// 模組授權輸入對話框請求模型。
/// </summary>
internal sealed class InSpecLicensePromptRequestModel
{
    /// <summary>
    /// 驗證狀態文字。
    /// </summary>
    public string StatusText { get; init; } = string.Empty;

    /// <summary>
    /// 對話框確認後要套用的授權值。
    /// </summary>
    public Action<string?> ApplySubmittedLicenseKey { get; init; } = _ => { };
}

/// <summary>
/// 訊息對話框請求模型。
/// </summary>
internal sealed class InSpecMessageRequestModel
{
    /// <summary>
    /// 對話框標題。
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 訊息內容。
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 對話框圖示型別。
    /// </summary>
    public InSpecMessageLevel Level { get; init; }

    /// <summary>
    /// 建立資訊訊息請求模型。
    /// </summary>
    /// <param name="title">對話框標題。</param>
    /// <param name="message">訊息內容。</param>
    /// <returns>資訊訊息請求模型。</returns>
    public static InSpecMessageRequestModel CreateInformation(string title, string message)
        => new() { Title = title, Message = message, Level = InSpecMessageLevel.Information };

    /// <summary>
    /// 建立警告訊息請求模型。
    /// </summary>
    /// <param name="title">對話框標題。</param>
    /// <param name="message">訊息內容。</param>
    /// <returns>警告訊息請求模型。</returns>
    public static InSpecMessageRequestModel CreateWarning(string title, string message)
        => new() { Title = title, Message = message, Level = InSpecMessageLevel.Warning };

    /// <summary>
    /// 建立錯誤訊息請求模型。
    /// </summary>
    /// <param name="title">對話框標題。</param>
    /// <param name="message">訊息內容。</param>
    /// <returns>錯誤訊息請求模型。</returns>
    public static InSpecMessageRequestModel CreateError(string title, string message)
        => new() { Title = title, Message = message, Level = InSpecMessageLevel.Error };
}

/// <summary>
/// 訊息對話框等級列舉。
/// </summary>
internal enum InSpecMessageLevel
{
    /// <summary>
    /// 資訊訊息。
    /// </summary>
    Information,

    /// <summary>
    /// 警告訊息。
    /// </summary>
    Warning,

    /// <summary>
    /// 錯誤訊息。
    /// </summary>
    Error
}




