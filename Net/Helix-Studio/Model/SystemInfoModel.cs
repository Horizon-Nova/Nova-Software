namespace Helix_Studio.Model.SystemInfo;

/// <summary>首頁系統資訊區塊所用之硬體與網路快照。</summary>
public sealed class HomeSystemInfoSnapshot
{
    /// <summary>電腦名稱。</summary>
    public string MachineName { get; init; } = string.Empty;

    /// <summary>處理器名稱字串。</summary>
    public string ProcessorName { get; init; } = string.Empty;

    /// <summary>邏輯處理器數量。</summary>
    public int LogicalProcessorCount { get; init; }

    /// <summary>作業系統描述。</summary>
    public string OperatingSystem { get; init; } = string.Empty;

    /// <summary>網路介面卡快照清單。</summary>
    public IReadOnlyList<NetworkAdapterSnapshot> NetworkAdapters { get; init; } = [];
}

/// <summary>單一網路介面卡之顯示用快照。</summary>
public sealed class NetworkAdapterSnapshot
{
    /// <summary>介面卡名稱。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>介面卡描述。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>是否運作中。</summary>
    public bool IsOperational { get; init; }

    /// <summary>連線速度（位元／秒）。</summary>
    public long LinkSpeedBitsPerSecond { get; init; }

    /// <summary>實體位址（MAC）字串。</summary>
    public string PhysicalAddress { get; init; } = string.Empty;

    /// <summary>IPv4 位址清單。</summary>
    public IReadOnlyList<string> IPv4Addresses { get; init; } = [];
}
