using Helix_Studio.Mvvm;

namespace Helix_Studio.Models;

/// <summary>
/// 系統資源使用量。即時變動，繼承 ObservableObject。
/// </summary>
public sealed class SystemStats : ObservableObject
{
    private double _cpuUsagePercent;
    private double _memoryUsedMb;
    private double _memoryTotalMb;

    /// <summary>目前 CPU 使用率（百分比）。</summary>
    public double CpuUsagePercent
    {
        get => _cpuUsagePercent;
        set => SetProperty(ref _cpuUsagePercent, value);
    }

    /// <summary>已使用記憶體（MB）。</summary>
    public double MemoryUsedMb
    {
        get => _memoryUsedMb;
        set => SetProperty(ref _memoryUsedMb, value);
    }

    /// <summary>總記憶體（MB）。</summary>
    public double MemoryTotalMb
    {
        get => _memoryTotalMb;
        set
        {
            if (SetProperty(ref _memoryTotalMb, value))
                OnPropertyChanged(nameof(MemoryTotalGb));
        }
    }

    /// <summary>總記憶體（GB），供顯示用。</summary>
    public double MemoryTotalGb => _memoryTotalMb / 1024.0;
}
