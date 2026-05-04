using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ExternalModules.InSpecTool.Model;

/// <summary>
/// 表示條碼解析後的結果快照。
/// </summary>
internal sealed class InSpecBarcodeParseResultModel
{
    /// <summary>
    /// 原始條碼內容。
    /// </summary>
    public string RawBarcode { get; init; } = string.Empty;

    /// <summary>
    /// 清理後的條碼內容。
    /// </summary>
    public string CleanedBarcode { get; init; } = string.Empty;

    /// <summary>
    /// 條碼分段內容清單。
    /// </summary>
    public IReadOnlyList<string> Parts { get; init; } = [];

    /// <summary>
    /// 依欄位順序解析出的欄位值清單。
    /// </summary>
    public IReadOnlyList<string> FieldValues { get; init; } = [];

    /// <summary>
    /// 是否曾輸入任何條碼內容。
    /// </summary>
    public bool HasInput => !string.IsNullOrWhiteSpace(RawBarcode);

    /// <summary>
    /// 是否包含不允許的字元。
    /// </summary>
    public bool HasBlockedCharacter { get; init; }

    /// <summary>
    /// 是否包含前置分隔符。
    /// </summary>
    public bool HasLeadingSeparator { get; init; }
}

/// <summary>
/// 表示單一欄位的定義設定。
/// </summary>
internal sealed class InSpecFieldDefinitionModel
{
    /// <summary>
    /// 直接輸入模式字串。
    /// </summary>
    public const string DirectInputTargetMode = "直接輸入";

    /// <summary>
    /// 提示確認模式字串。
    /// </summary>
    public const string PromptConfirmationTargetMode = "提示確認";

    /// <summary>
    /// 欄位名稱。
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 欄位關鍵字。
    /// </summary>
    public string Keyword { get; init; } = string.Empty;

    /// <summary>
    /// 欄位是否可編輯。
    /// </summary>
    public bool IsEditable { get; init; } = true;

    /// <summary>
    /// 欄位目標模式。
    /// </summary>
    public string TargetMode { get; init; } = DirectInputTargetMode;
}

/// <summary>
/// 表示 InSpecTool 執行時設定快照。
/// </summary>
internal sealed class InSpecToolConfigurationModel
{
    /// <summary>
    /// 條碼輸入防抖延遲毫秒數。
    /// </summary>
    public int BarcodeDebounceMs { get; init; } = 80;

    /// <summary>
    /// 是否啟用自動監控。
    /// </summary>
    public bool EnableAutoMonitoring { get; init; } = true;

    /// <summary>
    /// 自動監控輪詢間隔毫秒數。
    /// </summary>
    public int MonitorIntervalMs { get; init; } = 500;

    /// <summary>
    /// 是否啟用自動執行。
    /// </summary>
    public bool EnableAutoRun { get; init; } = true;

    /// <summary>
    /// 搜尋資料夾路徑。
    /// </summary>
    public string SearchDirectory { get; init; } = string.Empty;

    /// <summary>
    /// 系統記錄資料夾路徑。
    /// </summary>
    public string SystemLogDirectory { get; init; } = string.Empty;

    /// <summary>
    /// 備援儲存資料夾路徑。
    /// </summary>
    public string BackupSavePath { get; init; } = string.Empty;

    /// <summary>
    /// 搜尋檔名必要關鍵字。
    /// </summary>
    public string SetupKeyword { get; init; } = "SETUP";

    /// <summary>
    /// 欄位定義集合。
    /// </summary>
    public IReadOnlyList<InSpecFieldDefinitionModel> FieldDefinitions { get; init; } = [];
}

/// <summary>
/// 主頁面欄位顯示模型。
/// </summary>
internal sealed class InSpecPreviewFieldModel : INotifyPropertyChanged
{
    private string _value = string.Empty;

    /// <summary>
    /// 欄位顯示標籤。
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// 欄位值。
    /// </summary>
    public string Value
    {
        get => _value;
        set
        {
            var normalizedValue = value ?? string.Empty;
            if (string.Equals(_value, normalizedValue, StringComparison.Ordinal))
            {
                return;
            }

            _value = normalizedValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }

    /// <summary>
    /// 欄位是否唯讀。
    /// </summary>
    public bool IsReadOnly { get; init; }

    /// <summary>
    /// 欄位關鍵字。
    /// </summary>
    public string Keyword { get; init; } = string.Empty;

    /// <summary>
    /// 欄位目標模式。
    /// </summary>
    public string TargetMode { get; init; } = InSpecFieldDefinitionModel.DirectInputTargetMode;

    /// <summary>
    /// 屬性變更通知事件。
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// 主頁面檔案查詢結果模型。
/// </summary>
internal sealed class InSpecPreviewResultModel
{
    /// <summary>
    /// 顯示用檔名。
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// 對應料號。
    /// </summary>
    public string PartNumber { get; init; } = string.Empty;

    /// <summary>
    /// 對應版本字串。
    /// </summary>
    public string Revision { get; init; } = string.Empty;

    /// <summary>
    /// 實際檔案路徑。
    /// </summary>
    public string FilePath { get; init; } = string.Empty;
}

/// <summary>
/// 設定頁欄位結構列模型。
/// </summary>
internal sealed class InSpecSettingsFieldRowModel : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _keyword = string.Empty;
    private string _targetMode = InSpecFieldDefinitionModel.DirectInputTargetMode;
    private bool _isEditable = true;

    /// <summary>
    /// 欄位名稱。
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (string.Equals(_name, value, StringComparison.Ordinal))
            {
                return;
            }

            _name = value ?? string.Empty;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    /// <summary>
    /// 欄位關鍵字。
    /// </summary>
    public string Keyword
    {
        get => _keyword;
        set
        {
            if (string.Equals(_keyword, value, StringComparison.Ordinal))
            {
                return;
            }

            _keyword = value ?? string.Empty;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Keyword)));
        }
    }

    /// <summary>
    /// 欄位目標模式。
    /// </summary>
    public string TargetMode
    {
        get => _targetMode;
        set
        {
            if (string.Equals(_targetMode, value, StringComparison.Ordinal))
            {
                return;
            }

            _targetMode = value ?? string.Empty;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TargetMode)));
        }
    }

    /// <summary>
    /// 欄位是否可編輯。
    /// </summary>
    public bool IsEditable
    {
        get => _isEditable;
        set
        {
            if (_isEditable == value)
            {
                return;
            }

            _isEditable = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEditable)));
        }
    }

    /// <summary>
    /// 屬性變更通知事件。
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// 設定頁顯示所需的選項集合。
/// </summary>
internal sealed class InSpecSettingsOptionModel
{
    /// <summary>
    /// 條碼輸入延遲選項。
    /// </summary>
    public IReadOnlyList<string> ScanIntervalOptions { get; init; } = [];

    /// <summary>
    /// 自動監控輪詢間隔選項。
    /// </summary>
    public IReadOnlyList<string> MonitorIntervalOptions { get; init; } = [];

    /// <summary>
    /// 欄位目標模式選項。
    /// </summary>
    public IReadOnlyList<string> TargetModeOptions { get; init; } = [];
}

/// <summary>
/// 設定頁載入後的資料快照。
/// </summary>
internal sealed class InSpecSettingsSnapshotModel
{
    /// <summary>
    /// 條碼輸入延遲值。
    /// </summary>
    public string ScanIntervalMs { get; init; } = "80";

    /// <summary>
    /// 自動監控輪詢間隔值。
    /// </summary>
    public string MonitorIntervalMs { get; init; } = "500";

    /// <summary>
    /// 是否啟用自動監控。
    /// </summary>
    public bool EnableAutoMonitoring { get; init; } = true;

    /// <summary>
    /// 是否啟用自動執行。
    /// </summary>
    public bool EnableAutoRun { get; init; } = true;

    /// <summary>
    /// 搜尋資料夾路徑。
    /// </summary>
    public string SearchDirectory { get; init; } = string.Empty;

    /// <summary>
    /// 系統記錄資料夾路徑。
    /// </summary>
    public string SystemLogDirectory { get; init; } = string.Empty;

    /// <summary>
    /// 備援儲存資料夾路徑。
    /// </summary>
    public string BackupSavePath { get; init; } = string.Empty;

    /// <summary>
    /// 欄位結構列集合。
    /// </summary>
    public ObservableCollection<InSpecSettingsFieldRowModel> FieldRows { get; init; } = [];
}

/// <summary>
/// 設定儲存作業結果模型。
/// </summary>
internal sealed class InSpecSettingSaveResultModel
{
    /// <summary>
    /// 是否儲存成功。
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 儲存失敗時的訊息內容。
    /// </summary>
    public string DetailText { get; init; } = string.Empty;
}

