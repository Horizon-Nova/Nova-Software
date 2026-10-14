using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Vaeron.VNCSCP.Internal.Contracts;
using Vaeron.VNCSCP.Internal.Wpf.Components;

namespace Vaeron.VNCSCP.Dashboard;

/// <summary>主畫面的一列連線。顯示文字全部取自快照，這裡只多畫面自己的可變狀態與編輯欄位。</summary>
public sealed partial class DashboardConnectionRow : ObservableValidator, ISelectableRow
{
    [ObservableProperty]
    private DashboardRowSnapshot _snapshot;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isTesting;

    /// <summary>批次操作共用的勾選狀態，單一事實來源。</summary>
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string? _editGroupName;

    private string _editName = "";
    private string _editIp = "";
    private int _editPort = 5900;

    public DashboardConnectionRow(DashboardRowSnapshot snapshot)
    {
        _snapshot = snapshot;
        ApplyResetEditFields();
    }

    public int Id => Snapshot.Connection.Id;
    public string Name => Snapshot.Connection.Name;
    public string Ip => Snapshot.Connection.Ip;
    public int Port => Snapshot.Connection.Port;
    public ConnectionStatus Status => Snapshot.Connection.Status;
    public string StatusDisplayText => Snapshot.Connection.StatusText;
    public string LastTestedDisplay => Snapshot.Connection.LastTestedDisplay;
    public string? GroupName => Snapshot.Connection.GroupName;

    /// <summary>清單分段用的群組鍵。未分組的顯示文字由 Vw_Connection 決定。</summary>
    public string GroupDisplayName => Snapshot.Connection.GroupDisplayName;

    public string DeployStateText => Snapshot.DeployStateText;
    public string DeployMessage => Snapshot.DeployMessage;
    public bool CanRetryDeploy => Snapshot.CanRetryDeploy;
    public string? LastDeployTaskId => Snapshot.LastDeployTaskId;
    public IReadOnlyList<EventSnapshot> RecentEvents => Snapshot.RecentEvents;

    [Required(ErrorMessage = "名稱必填")]
    [MaxLength(50, ErrorMessage = "名稱上限 50 字")]
    public string EditName
    {
        get => _editName;
        set => SetProperty(ref _editName, value, true);
    }

    [Required(ErrorMessage = "IP 必填")]
    [RegularExpression(@"^(\d{1,3}\.){3}\d{1,3}$", ErrorMessage = "IP 格式錯誤")]
    public string EditIp
    {
        get => _editIp;
        set => SetProperty(ref _editIp, value, true);
    }

    [Range(1, 65535, ErrorMessage = "Port 範圍需為 1-65535")]
    public int EditPort
    {
        get => _editPort;
        set => SetProperty(ref _editPort, value, true);
    }

    /// <summary>把編輯欄位還原成目前連線的值。</summary>
    public void ApplyResetEditFields()
    {
        EditName = Snapshot.Connection.Name;
        EditIp = Snapshot.Connection.Ip;
        EditPort = Snapshot.Connection.Port;
        EditGroupName = Snapshot.Connection.GroupName;
    }

    public void ApplyValidateAll() => ValidateAllProperties();

    /// <summary>部署進度直接改這幾個欄位，不整批重查。</summary>
    public void ApplyDeployProgress(DeployItemProgress progress)
        => Snapshot = Snapshot with
        {
            DeployStateText = progress.StateText,
            DeployMessage = progress.Message,
            CanRetryDeploy = progress.State == DeployItemState.Failed,
            LastDeployTaskId = progress.TaskId
        };

    public void ApplyRecentEvents(IReadOnlyList<EventSnapshot> events)
        => Snapshot = Snapshot with { RecentEvents = events };

    partial void OnSnapshotChanged(DashboardRowSnapshot value)
    {
        var projected = new[]
        {
            nameof(Id), nameof(Name), nameof(Ip), nameof(Port), nameof(Status), nameof(StatusDisplayText),
            nameof(LastTestedDisplay), nameof(GroupName), nameof(GroupDisplayName), nameof(DeployStateText),
            nameof(DeployMessage), nameof(CanRetryDeploy), nameof(LastDeployTaskId), nameof(RecentEvents)
        };

        foreach (var name in projected)
        {
            OnPropertyChanged(name);
        }
    }
}

