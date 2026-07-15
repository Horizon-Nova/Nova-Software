using CommunityToolkit.Mvvm.ComponentModel;
using Vaeron.HelixStudio.Features.RemoteBatchTransfer.Contracts;

namespace Vaeron.HelixStudio.Features.RemoteBatchTransfer;

/// <summary>畫面用的選取狀態包裝——「是否勾選」是純 UI 狀態，不屬於 Contracts 的 Snapshot
/// （Snapshot 是唯讀資料契約，不含可變 UI 狀態），故不放進 Contracts，只在本 Feature 的
/// ViewModel/View 之間使用。</summary>
public sealed partial class SelectableConnection(RemoteConnectionSnapshot connection) : ObservableObject
{
    public RemoteConnectionSnapshot Connection { get; } = connection;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string? _destPathOverride;
}
