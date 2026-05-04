using System.Collections.ObjectModel;
using System.Windows.Input;
using ExternalModules.InSpecTool.Model;
using ExternalModules.InSpecTool.Services;
using ExternalModules.InSpecTool.ViewModels.Base;

namespace ExternalModules.InSpecTool.ViewModels;

/// <summary>
/// InSpecTool 設定頁的 UI 狀態機。
/// </summary>
internal sealed class InSpecToolSettingsPageViewModel : ViewModelBase
{
    private readonly InSpecToolServices _services;
    private bool _isBusy;
    private bool _isDirty;
    private bool _isLoading;
    private string _statusText = string.Empty;
    private string _scanIntervalMs = "80";
    private string _monitorIntervalMs = "500";
    private bool _enableAutoMonitoring = true;
    private bool _enableAutoRun = true;
    private string _searchDirectory = string.Empty;
    private string _systemLogDirectory = string.Empty;
    private string _backupSavePath = string.Empty;

    /// <summary>
    /// 建立設定頁 ViewModel。
    /// </summary>
    /// <param name="services">服務層實例。</param>
    public InSpecToolSettingsPageViewModel(InSpecToolServices services)
    {
        _services = services;
        FieldRows = [];

        var optionModel = _services.QuerySettingsOptionModel();
        ScanIntervalOptions = optionModel.ScanIntervalOptions;
        MonitorIntervalOptions = optionModel.MonitorIntervalOptions;
        TargetModeOptions = optionModel.TargetModeOptions;

        ApplyAddFieldRowCommand = new RelayCommand(ApplyAddFieldRow);
        DeleteFieldRowCommand = new RelayCommand(ApplyDeleteFieldRow);
        SaveFieldSchemaCommand = new RelayCommand(ApplySaveFieldSchema);
        ApplySearchDirectoryDialogCommand = new RelayCommand(ApplySearchDirectoryDialog);
        ApplySystemLogDirectoryDialogCommand = new RelayCommand(ApplySystemLogDirectoryDialog);
        ApplyBackupSavePathDialogCommand = new RelayCommand(ApplyBackupSavePathDialog);
    }

    /// <summary>
    /// 條碼輸入延遲選項集合。
    /// </summary>
    public IReadOnlyList<string> ScanIntervalOptions { get; }

    /// <summary>
    /// 自動監控輪詢間隔選項集合。
    /// </summary>
    public IReadOnlyList<string> MonitorIntervalOptions { get; }

    /// <summary>
    /// 欄位目標模式選項集合。
    /// </summary>
    public IReadOnlyList<string> TargetModeOptions { get; }

    /// <summary>
    /// 欄位結構列集合。
    /// </summary>
    public ObservableCollection<InSpecSettingsFieldRowModel> FieldRows { get; }

    /// <summary>
    /// 設定頁目前是否忙碌。
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>
    /// 設定頁是否存在未保存變更。
    /// </summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    /// <summary>
    /// 狀態訊息文字。
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value ?? string.Empty);
    }

    /// <summary>
    /// 條碼輸入延遲值。
    /// </summary>
    public string ScanIntervalMs
    {
        get => _scanIntervalMs;
        set
        {
            if (!SetProperty(ref _scanIntervalMs, value ?? string.Empty))
            {
                return;
            }

            ApplyStringSettingSave("scanIntervalMs", _scanIntervalMs, "已更新條碼輸入延遲。");
        }
    }

    /// <summary>
    /// 自動監控輪詢間隔值。
    /// </summary>
    public string MonitorIntervalMs
    {
        get => _monitorIntervalMs;
        set
        {
            if (!SetProperty(ref _monitorIntervalMs, value ?? string.Empty))
            {
                return;
            }

            ApplyStringSettingSave("monitorIntervalMs", _monitorIntervalMs, "已更新監控輪詢間隔。");
        }
    }

    /// <summary>
    /// 是否啟用自動監控。
    /// </summary>
    public bool EnableAutoMonitoring
    {
        get => _enableAutoMonitoring;
        set
        {
            if (!SetProperty(ref _enableAutoMonitoring, value))
            {
                return;
            }

            ApplyBooleanSettingSave("enableAutoMonitoring", value, "已更新自動監控設定。");
        }
    }

    /// <summary>
    /// 是否啟用自動執行。
    /// </summary>
    public bool EnableAutoRun
    {
        get => _enableAutoRun;
        set
        {
            if (!SetProperty(ref _enableAutoRun, value))
            {
                return;
            }

            ApplyBooleanSettingSave("enableAutoRun", value, "已更新自動執行設定。");
        }
    }

    /// <summary>
    /// 搜尋資料夾路徑。
    /// </summary>
    public string SearchDirectory
    {
        get => _searchDirectory;
        set
        {
            if (!SetProperty(ref _searchDirectory, value ?? string.Empty))
            {
                return;
            }

            ApplyStringSettingSave("searchDirectory", _searchDirectory, "已更新搜尋目錄。");
        }
    }

    /// <summary>
    /// 系統記錄資料夾路徑。
    /// </summary>
    public string SystemLogDirectory
    {
        get => _systemLogDirectory;
        set
        {
            if (!SetProperty(ref _systemLogDirectory, value ?? string.Empty))
            {
                return;
            }

            ApplyStringSettingSave("systemLogDirectory", _systemLogDirectory, "已更新系統記錄目錄。");
        }
    }

    /// <summary>
    /// 備援儲存資料夾路徑。
    /// </summary>
    public string BackupSavePath
    {
        get => _backupSavePath;
        set
        {
            if (!SetProperty(ref _backupSavePath, value ?? string.Empty))
            {
                return;
            }

            ApplyStringSettingSave("backupSavePath", _backupSavePath, "已更新備援儲存目錄。");
        }
    }

    /// <summary>
    /// 新增欄位結構列的命令。
    /// </summary>
    public ICommand ApplyAddFieldRowCommand { get; }

    /// <summary>
    /// 刪除欄位結構列的命令。
    /// </summary>
    public ICommand DeleteFieldRowCommand { get; }

    /// <summary>
    /// 儲存欄位結構設定的命令。
    /// </summary>
    public ICommand SaveFieldSchemaCommand { get; }

    /// <summary>
    /// 套用搜尋目錄對話框結果的命令。
    /// </summary>
    public ICommand ApplySearchDirectoryDialogCommand { get; }

    /// <summary>
    /// 套用系統記錄目錄對話框結果的命令。
    /// </summary>
    public ICommand ApplySystemLogDirectoryDialogCommand { get; }

    /// <summary>
    /// 套用備援儲存目錄對話框結果的命令。
    /// </summary>
    public ICommand ApplyBackupSavePathDialogCommand { get; }

    /// <summary>
    /// 依目前模組上下文重新載入設定頁資料。
    /// </summary>
    internal void LoadSettings()
    {
        _isLoading = true;
        try
        {
            IsBusy = true;
            var snapshot = _services.QuerySettingsSnapshot();
            _scanIntervalMs = snapshot.ScanIntervalMs;
            _monitorIntervalMs = snapshot.MonitorIntervalMs;
            _enableAutoMonitoring = snapshot.EnableAutoMonitoring;
            _enableAutoRun = snapshot.EnableAutoRun;
            _searchDirectory = snapshot.SearchDirectory;
            _systemLogDirectory = snapshot.SystemLogDirectory;
            _backupSavePath = snapshot.BackupSavePath;

            RaisePropertyChanged(nameof(ScanIntervalMs));
            RaisePropertyChanged(nameof(MonitorIntervalMs));
            RaisePropertyChanged(nameof(EnableAutoMonitoring));
            RaisePropertyChanged(nameof(EnableAutoRun));
            RaisePropertyChanged(nameof(SearchDirectory));
            RaisePropertyChanged(nameof(SystemLogDirectory));
            RaisePropertyChanged(nameof(BackupSavePath));

            FieldRows.Clear();
            foreach (var fieldRow in snapshot.FieldRows)
            {
                FieldRows.Add(fieldRow);
            }

            StatusText = "已載入目前模組設定。";
            IsDirty = false;
        }
        finally
        {
            IsBusy = false;
            _isLoading = false;
        }
    }

    private void ApplyAddFieldRow()
    {
        FieldRows.Add(_services.CreateSettingsFieldRow());
        IsDirty = true;
        StatusText = "已新增欄位列，記得按「儲存欄位結構」。";
    }

    private void ApplyDeleteFieldRow(object? parameter)
    {
        if (parameter is not InSpecSettingsFieldRowModel fieldRow)
        {
            return;
        }

        FieldRows.Remove(fieldRow);
        IsDirty = true;
        StatusText = "已移除欄位列，記得按「儲存欄位結構」。";
    }

    private void ApplySaveFieldSchema()
    {
        IsBusy = true;
        var schemaText = _services.ResolveFieldSchemaText(FieldRows);
        var saveResult = _services.SaveStringSetting("fieldSchema", schemaText);
        IsBusy = false;
        if (!saveResult.IsSuccess)
        {
            StatusText = $"儲存失敗：{saveResult.DetailText}";
            return;
        }

        IsDirty = false;
        StatusText = "已更新欄位結構。";
    }

    private void ApplySearchDirectoryDialog(object? parameter)
    {
        if (parameter is string path)
        {
            SearchDirectory = path;
        }
    }

    private void ApplySystemLogDirectoryDialog(object? parameter)
    {
        if (parameter is string path)
        {
            SystemLogDirectory = path;
        }
    }

    private void ApplyBackupSavePathDialog(object? parameter)
    {
        if (parameter is string path)
        {
            BackupSavePath = path;
        }
    }

    private void ApplyStringSettingSave(string key, string? value, string successMessage)
    {
        if (_isLoading)
        {
            return;
        }

        IsBusy = true;
        IsDirty = true;
        var saveResult = _services.SaveStringSetting(key, value);
        IsBusy = false;
        if (!saveResult.IsSuccess)
        {
            StatusText = $"儲存失敗：{saveResult.DetailText}";
            return;
        }

        IsDirty = false;
        StatusText = successMessage;
    }

    private void ApplyBooleanSettingSave(string key, bool value, string successMessage)
    {
        if (_isLoading)
        {
            return;
        }

        IsBusy = true;
        IsDirty = true;
        var saveResult = _services.SaveBooleanSetting(key, value);
        IsBusy = false;
        if (!saveResult.IsSuccess)
        {
            StatusText = $"儲存失敗：{saveResult.DetailText}";
            return;
        }

        IsDirty = false;
        StatusText = successMessage;
    }
}
