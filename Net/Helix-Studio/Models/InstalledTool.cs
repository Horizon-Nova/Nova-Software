using Helix_Studio.Mvvm;

namespace Helix_Studio.Models;

/// <summary>
/// 已安裝工具的執行期/UI 物件：結合 manifest（靜態）、安裝紀錄（持久）與執行狀態（暫時）。
/// 應用程式頁綁定此物件；狀態與 CPU% 會即時變動，故繼承 ObservableObject。
/// </summary>
public sealed class InstalledTool : ObservableObject
{
    private AppStatus _status = AppStatus.Stopped;
    private string _statusMessage = string.Empty;
    private double _cpuPercent;
    private double _memoryMb;
    private DateTime? _lastStarted;
    private bool _isSelected;

    public required ToolManifest Manifest { get; init; }
    public required DateTime InstalledAt { get; init; }
    public ToolSettings Settings { get; set; } = new();

    public string Id => Manifest.Id;
    public string Name => Manifest.Name;

    public AppStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
                OnPropertyChanged(nameof(IsRunning));
        }
    }

    /// <summary>工具回報的補充訊息（如錯誤描述或鎖定原因）。</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsRunning => _status is AppStatus.Starting or AppStatus.Running;

    public double CpuPercent
    {
        get => _cpuPercent;
        set => SetProperty(ref _cpuPercent, value);
    }

    /// <summary>即時實體記憶體用量（MB）；由 ProcessSampler 取樣。</summary>
    public double MemoryMb
    {
        get => _memoryMb;
        set => SetProperty(ref _memoryMb, value);
    }

    public DateTime? LastStarted
    {
        get => _lastStarted;
        set => SetProperty(ref _lastStarted, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
