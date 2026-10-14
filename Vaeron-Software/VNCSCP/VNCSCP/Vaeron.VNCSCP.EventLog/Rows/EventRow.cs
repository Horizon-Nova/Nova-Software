using CommunityToolkit.Mvvm.ComponentModel;
using Vaeron.VNCSCP.Internal.Contracts;

namespace Vaeron.VNCSCP.EventLog;

/// <summary>事件清單的一列。顯示文字全部取自快照，這裡只多兩個畫面自己的可變狀態。</summary>
public sealed partial class EventRow : ObservableObject
{
    public required EventSnapshot Snapshot { get; init; }

    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>複製明細後的短暫回饋。</summary>
    [ObservableProperty]
    private bool _isJustCopied;

    public EventLevel Level => Snapshot.Level;
    public string LevelText => Snapshot.LevelText;
    public string Source => Snapshot.Source;
    public string Message => Snapshot.Message;
    public string Detail => Snapshot.Detail;
    public bool HasDetail => Snapshot.HasDetail;
    public string OccurredDisplay => Snapshot.OccurredDisplay;
}
