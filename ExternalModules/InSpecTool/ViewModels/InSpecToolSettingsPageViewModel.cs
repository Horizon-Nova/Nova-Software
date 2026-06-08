using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ExternalModules.InSpecTool.App.Runtime;
using ExternalModules.InSpecTool.Model;
using ExternalModules.InSpecTool.ViewModels.Base;

namespace ExternalModules.InSpecTool.ViewModels;

internal sealed class InSpecToolSettingsPageViewModel : ViewModelBase, IDisposable
{
    private const double UnlockWindowMinutes = 3;

    private readonly InSpecToolRuntime _services;
    private readonly System.Timers.Timer _unlockTimer = new(TimeSpan.FromMinutes(UnlockWindowMinutes).TotalMilliseconds)
    {
        AutoReset = false
    };

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
    private string _moduleLicenseKey = string.Empty;
    private bool _isFieldSchemaLocked = true;
    private string _unlockStatusText = "欄位設定已鎖定。";

    public InSpecToolSettingsPageViewModel(InSpecToolRuntime services)
    {
        _services = services;
        FieldRows = [];
        _unlockTimer.Elapsed += ApplyUnlockExpired;

        var optionModel = _services.QuerySettingsOptionModel();
        ScanIntervalOptions = optionModel.ScanIntervalOptions;
        MonitorIntervalOptions = optionModel.MonitorIntervalOptions;
        TargetModeOptions = optionModel.TargetModeOptions;

        ApplyAddFieldRowCommand = new RelayCommand(ApplyAddFieldRow, () => false);
        DeleteFieldRowCommand = new RelayCommand(ApplyDeleteFieldRow, _ => false);
        SaveFieldSchemaCommand = new RelayCommand(ApplySaveFieldSchema, () => IsFieldSchemaUnlocked && !IsBusy);
        ApplyModuleLicenseKeyCommand = new RelayCommand(ApplyModuleLicenseKey, () => !IsBusy);
        ApplySearchDirectoryDialogCommand = new RelayCommand(ApplySearchDirectoryDialog);
        ApplySystemLogDirectoryDialogCommand = new RelayCommand(ApplySystemLogDirectoryDialog);
        ApplyBackupSavePathDialogCommand = new RelayCommand(ApplyBackupSavePathDialog);
    }

    public IReadOnlyList<string> ScanIntervalOptions { get; }

    public IReadOnlyList<string> MonitorIntervalOptions { get; }

    public IReadOnlyList<string> TargetModeOptions { get; }

    public ObservableCollection<InSpecSettingsFieldRowItemViewModel> FieldRows { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RelayCommand.ApplyInvalidate();
            }
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value ?? string.Empty);
    }

    public string ScanIntervalMs
    {
        get => _scanIntervalMs;
        set
        {
            if (!SetProperty(ref _scanIntervalMs, value ?? string.Empty))
            {
                return;
            }

            ApplyStringSettingSave("scanIntervalMs", _scanIntervalMs, "掃描間隔已儲存。");
        }
    }

    public string MonitorIntervalMs
    {
        get => _monitorIntervalMs;
        set
        {
            if (!SetProperty(ref _monitorIntervalMs, value ?? string.Empty))
            {
                return;
            }

            ApplyStringSettingSave("monitorIntervalMs", _monitorIntervalMs, "監控間隔已儲存。");
        }
    }

    public bool EnableAutoMonitoring
    {
        get => _enableAutoMonitoring;
        set
        {
            if (!SetProperty(ref _enableAutoMonitoring, value))
            {
                return;
            }

            ApplyBooleanSettingSave("enableAutoMonitoring", value, "自動監控設定已儲存。");
        }
    }

    public bool EnableAutoRun
    {
        get => _enableAutoRun;
        set
        {
            if (!SetProperty(ref _enableAutoRun, value))
            {
                return;
            }

            ApplyBooleanSettingSave("enableAutoRun", value, "自動執行 Run 設定已儲存。");
        }
    }

    public string SearchDirectory
    {
        get => _searchDirectory;
        set
        {
            if (!SetProperty(ref _searchDirectory, value ?? string.Empty))
            {
                return;
            }

            ApplyStringSettingSave("searchDirectory", _searchDirectory, "搜尋資料夾已儲存。");
        }
    }

    public string SystemLogDirectory
    {
        get => _systemLogDirectory;
        set
        {
            if (!SetProperty(ref _systemLogDirectory, value ?? string.Empty))
            {
                return;
            }

            ApplyStringSettingSave("systemLogDirectory", _systemLogDirectory, "系統輸出路徑已儲存。");
        }
    }

    public string BackupSavePath
    {
        get => _backupSavePath;
        set
        {
            if (!SetProperty(ref _backupSavePath, value ?? string.Empty))
            {
                return;
            }

            ApplyStringSettingSave("backupSavePath", _backupSavePath, "備援輸出路徑已儲存。");
        }
    }

    public string ModuleLicenseKey
    {
        get => _moduleLicenseKey;
        set
        {
            if (!SetProperty(ref _moduleLicenseKey, value ?? string.Empty))
            {
                return;
            }

            RaisePropertyChanged(nameof(UnlockKeyInput));
        }
    }

    public string UnlockKeyInput
    {
        get => _moduleLicenseKey;
        set => ModuleLicenseKey = value;
    }

    public string UnlockStatusText
    {
        get => _unlockStatusText;
        private set => SetProperty(ref _unlockStatusText, value ?? string.Empty);
    }

    public string LockStateText => IsFieldSchemaLocked ? "欄位設定已鎖定。" : "欄位設定已解鎖。";

    public bool IsFieldSchemaLocked
    {
        get => _isFieldSchemaLocked;
        private set
        {
            if (SetProperty(ref _isFieldSchemaLocked, value))
            {
                RaisePropertyChanged(nameof(IsFieldSchemaUnlocked));
                RaisePropertyChanged(nameof(LockStateText));
                RaisePropertyChanged(nameof(LicenseStatusText));
                RelayCommand.ApplyInvalidate();
            }
        }
    }

    public bool IsFieldSchemaUnlocked => !IsFieldSchemaLocked;

    public string LicenseStatusText => IsFieldSchemaLocked
        ? "授權未設定或無效"
        : "授權有效";

    public bool IsFixedFieldSchema => true;

    public ICommand ApplyAddFieldRowCommand { get; }

    public ICommand DeleteFieldRowCommand { get; }

    public ICommand SaveFieldSchemaCommand { get; }

    public ICommand ApplyModuleLicenseKeyCommand { get; }

    public ICommand ApplyUnlockCommand => ApplyModuleLicenseKeyCommand;

    public ICommand ApplySearchDirectoryDialogCommand { get; }

    public ICommand ApplySystemLogDirectoryDialogCommand { get; }

    public ICommand ApplyBackupSavePathDialogCommand { get; }

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
            _moduleLicenseKey = string.Empty;

            RaisePropertyChanged(nameof(ScanIntervalMs));
            RaisePropertyChanged(nameof(MonitorIntervalMs));
            RaisePropertyChanged(nameof(EnableAutoMonitoring));
            RaisePropertyChanged(nameof(EnableAutoRun));
            RaisePropertyChanged(nameof(SearchDirectory));
            RaisePropertyChanged(nameof(SystemLogDirectory));
            RaisePropertyChanged(nameof(BackupSavePath));
            RaisePropertyChanged(nameof(ModuleLicenseKey));
            RaisePropertyChanged(nameof(UnlockKeyInput));

            FieldRows.Clear();
            foreach (var fieldRow in snapshot.FieldRows)
            {
                FieldRows.Add(InSpecSettingsFieldRowItemViewModel.Create(fieldRow));
            }

            _unlockTimer.Stop();
            IsFieldSchemaLocked = true;
            StatusText = BuildFieldSchemaStatusText(isLocked: true);
            UnlockStatusText = BuildUnlockStatusText(isLocked: true);
            IsDirty = false;
        }
        finally
        {
            IsBusy = false;
            _isLoading = false;
        }
    }

    public void Dispose()
    {
        _unlockTimer.Elapsed -= ApplyUnlockExpired;
        _unlockTimer.Dispose();
    }

    private void ApplyAddFieldRow()
    {
        StatusText = "欄位結構已固定，不能新增欄位。";
    }

    private void ApplyDeleteFieldRow(object? _)
    {
        StatusText = "欄位結構已固定，不能刪除欄位。";
    }

    private void ApplySaveFieldSchema()
    {
        if (IsFieldSchemaLocked)
        {
            StatusText = "固定欄位結構目前鎖定，請先在右側完成暫時解鎖。";
            return;
        }

        IsBusy = true;
        var schemaText = _services.ResolveFieldSchemaText(FieldRows.Select(fieldRow => fieldRow.BuildModel()));
        var saveResult = _services.SaveStringSetting("fieldSchema", schemaText);
        IsBusy = false;

        if (!saveResult.IsSuccess)
        {
            StatusText = $"固定欄位設定儲存失敗：{saveResult.DetailText}";
            return;
        }

        IsDirty = false;
        StatusText = "固定欄位的行為設定已儲存。";
    }

    private void ApplyModuleLicenseKey()
    {
        if (_isLoading)
        {
            return;
        }

        IsBusy = true;
        var verification = _services.VerifyModuleLicenseKey(ModuleLicenseKey.Trim());
        IsBusy = false;
        ModuleLicenseKey = string.Empty;

        if (!verification.IsValid)
        {
            _unlockTimer.Stop();
            IsFieldSchemaLocked = true;
            StatusText = string.IsNullOrWhiteSpace(verification.ErrorMessage)
                ? "敏感欄位解鎖失敗：輸入的模組金鑰無效。"
                : $"敏感欄位解鎖失敗：{verification.ErrorMessage}";
            UnlockStatusText = BuildUnlockStatusText(isLocked: true);
            return;
        }

        _unlockTimer.Stop();
        _unlockTimer.Start();
        IsFieldSchemaLocked = false;
        StatusText = BuildFieldSchemaStatusText(isLocked: false);
        UnlockStatusText = BuildUnlockStatusText(isLocked: false);
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
            StatusText = $"設定儲存失敗：{saveResult.DetailText}";
            return;
        }

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
            StatusText = $"設定儲存失敗：{saveResult.DetailText}";
            return;
        }

        StatusText = successMessage;
    }

    private static string BuildFieldSchemaStatusText(bool isLocked)
        => isLocked
            ? "固定欄位結構目前鎖定，請在右側輸入有效模組金鑰暫時解鎖。"
            : "固定欄位結構已載入，目前只允許調整欄位行為設定。";

    private static string BuildUnlockStatusText(bool isLocked)
        => isLocked
            ? "欄位設定已鎖定。"
            : $"欄位設定已解鎖，將於 {UnlockWindowMinutes:0} 分鐘後自動重新上鎖。";

    private void ApplyUnlockExpired(object? sender, System.Timers.ElapsedEventArgs eventArgs)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsFieldSchemaLocked = true;
            StatusText = BuildFieldSchemaStatusText(isLocked: true);
            UnlockStatusText = BuildUnlockStatusText(isLocked: true);
        });
    }
}
