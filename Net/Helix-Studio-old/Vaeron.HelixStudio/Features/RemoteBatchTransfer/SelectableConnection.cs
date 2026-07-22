using CommunityToolkit.Mvvm.ComponentModel;
using Vaeron.HelixStudio.Features.RemoteBatchTransfer.Contracts;

namespace Vaeron.HelixStudio.Features.RemoteBatchTransfer;

/// <summary>連線列的選取狀態包裝</summary>
public sealed partial class SelectableConnection(RemoteConnectionSnapshot connection) : ObservableObject
{
    public RemoteConnectionSnapshot Connection { get; } = connection;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string? _destPathOverride;
}
