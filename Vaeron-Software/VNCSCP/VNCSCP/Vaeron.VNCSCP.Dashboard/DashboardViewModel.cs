using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vaeron.Kernel;
using Vaeron.Kernel.Secrets;
using Vaeron.VNCSCP.Internal.Contracts;
using Vaeron.VNCSCP.Internal.Shell;
using Vaeron.VNCSCP.Internal.Wpf;

namespace Vaeron.VNCSCP.Dashboard;

/// <summary>主畫面的繫結來源。篩選條件送進查詢，畫面只顯示查回來的結果。</summary>
public sealed partial class DashboardViewModel(
    DashboardService service,
    INotificationService notification,
    IConfirmationService confirmation,
    INavigator navigator) : ViewModelBase
{
    private const int MaxRetainedDeployTasks = 20;

    public ObservableCollection<DashboardConnectionRow> Connections { get; } = [];

    /// <summary>群組下拉共用的資料來源，內容是目前正被使用的名稱。</summary>
    public ObservableCollection<string> Groups { get; } = [];

    public ObservableCollection<GroupRow> GroupManagementRows { get; } = [];

    public ObservableCollection<DeployTaskRow> DeployTasks { get; } = [];

    private ICollectionView? _connectionsView;

    public ICollectionView ConnectionsView => _connectionsView ??= CreateConnectionsView();

    public int SelectedCount => Connections.Count(row => row.IsSelected);

    public bool HasSelection => SelectedCount > 0;

    public bool HasNoDeployTasks => DeployTasks.Count == 0;

    private string _newName = "";
    private string _newIp = "";
    private int _newPort = 5900;

    [Required(ErrorMessage = "名稱必填")]
    [MaxLength(50, ErrorMessage = "名稱上限 50 字")]
    public string NewName
    {
        get => _newName;
        set => SetProperty(ref _newName, value, true);
    }

    [Required(ErrorMessage = "IP 必填")]
    [RegularExpression(@"^(\d{1,3}\.){3}\d{1,3}$", ErrorMessage = "IP 格式錯誤")]
    public string NewIp
    {
        get => _newIp;
        set => SetProperty(ref _newIp, value, true);
    }

    [Range(1, 65535, ErrorMessage = "Port 範圍需為 1-65535")]
    public int NewPort
    {
        get => _newPort;
        set => SetProperty(ref _newPort, value, true);
    }

    #region 控制項

    [ObservableProperty]
    private bool _hasNoConnections = true;

    [ObservableProperty]
    private bool _hasLoadError;

    [ObservableProperty]
    private string _loadErrorMessage = "";

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private bool _showOnlyAbnormal;

    [ObservableProperty]
    private bool _isAddRowExpanded;

    [ObservableProperty]
    private bool _isGroupPanelExpanded;

    [ObservableProperty]
    private bool _isDeployPanelExpanded;

    [ObservableProperty]
    private string? _newGroupName;

    [ObservableProperty]
    private string _deployLocalSourcePath = "";

    [ObservableProperty]
    private string _deployRemoteTargetPath = "";

    [ObservableProperty]
    private bool _isDeployScheduled;

    [ObservableProperty]
    private string _deployScheduledAtText = "";

    #endregion

    #region 生命週期

    public void ApplyViewLoaded()
    {
        service.Changed += OnConnectionsChanged;
        service.DeployProgressChanged += OnDeployProgressChanged;
        DeployTasks.CollectionChanged += OnDeployTasksChanged;
        RunSafe(ApplySnapshotAsync);
        RunSafe(ApplyLoadDeployPreferencesAsync);
    }

    public void ApplyViewUnloaded()
    {
        service.Changed -= OnConnectionsChanged;
        service.DeployProgressChanged -= OnDeployProgressChanged;
        DeployTasks.CollectionChanged -= OnDeployTasksChanged;
    }

    #endregion

    #region 事件回呼

    private void OnConnectionsChanged() => RunSafe(ApplySnapshotAsync);

    private void OnDeployTasksChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => OnPropertyChanged(nameof(HasNoDeployTasks));

    partial void OnSearchQueryChanged(string value) => RunSafe(ApplySnapshotAsync);

    partial void OnShowOnlyAbnormalChanged(bool value) => RunSafe(ApplySnapshotAsync);

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        foreach (var _ in new[] { e.PropertyName }.Where(name => name == nameof(DashboardConnectionRow.IsSelected)))
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    /// <summary>終態時多重查一次該連線的近期事件，因為部署剛寫入一筆。</summary>
    private void OnDeployProgressChanged(DeployItemProgress progress)
        => PostToUi(() =>
        {
            var rows = Connections.Where(row => row.Id == progress.ConnectionId).ToList();
            rows.ForEach(row => row.ApplyDeployProgress(progress));

            foreach (var task in DeployTasks.Where(item => item.TaskId == progress.TaskId))
            {
                task.ApplyItemState(progress.ConnectionId, progress.State);
            }

            var terminal = new[] { progress }
                .Where(item => item.State is DeployItemState.Success or DeployItemState.Failed or DeployItemState.Cancelled);

            foreach (var row in terminal.SelectMany(_ => rows))
            {
                RunSafe(() => ApplyRecentEventsAsync(row));
            }
        });

    #endregion

    #region 外部操作方法

    /// <summary>展開列時解出該台密碼供遮罩框顯示。解不出來時序列為空。</summary>
    public async Task<IReadOnlyList<string>> ApplyRevealPasswordAsync(int connectionId)
    {
        var revealed = await service.RevealPasswordAsync(connectionId, CancellationToken.None);

        foreach (var message in revealed.Failures)
        {
            notification.ShowResult(new OperationOutcome(false, message));
        }

        return revealed.Values.ToList();
    }

    /// <summary>儲存編輯，密碼留空代表不變更。回傳是否成功，供呼叫端決定要不要清空密碼框。</summary>
    public async Task<bool> ApplySaveEditAsync(DashboardConnectionRow row, SecretBuffer? password)
    {
        row.ApplyValidateAll();
        var saved = new List<Result>();

        foreach (var _ in new[] { row }.Where(item => item.HasErrors is false))
        {
            var request = new UpdateConnectionRequest
            {
                Id = row.Id,
                Name = row.EditName,
                Ip = row.EditIp,
                Port = row.EditPort,
                GroupName = NormalizedGroupName(row.EditGroupName)
            };

            var updated = await service.UpdateConnectionAsync(request, password, CancellationToken.None);
            notification.ShowResult(new OperationOutcome(updated.IsSuccess, TextOf(updated, "已更新")));
            saved.Add(updated);
        }

        return saved.Any(result => result.IsSuccess);
    }

    #endregion

    #region 命令

    [RelayCommand]
    private void ApplyReload() => RunSafe(ApplySnapshotAsync);

    [RelayCommand]
    private void ApplyToggleAddRow() => IsAddRowExpanded = IsAddRowExpanded is false;

    [RelayCommand]
    private void ApplyToggleGroupPanel() => IsGroupPanelExpanded = IsGroupPanelExpanded is false;

    [RelayCommand]
    private void ApplyToggleDeployPanel() => IsDeployPanelExpanded = IsDeployPanelExpanded is false;

    [RelayCommand]
    private void ApplyOpenEventLog() => navigator.NavigateTo("EventLog");

    [RelayCommand]
    private void ApplyToggleExpand(DashboardConnectionRow? row)
    {
        foreach (var target in new[] { row }.OfType<DashboardConnectionRow>())
        {
            foreach (var _ in new[] { target }.Where(item => item.IsExpanded is false))
            {
                target.ApplyResetEditFields();
            }

            target.IsExpanded = target.IsExpanded is false;
        }
    }

    [RelayCommand]
    private async Task ApplyCreateConnectionAsync(SecretBuffer password)
    {
        using var owned = password;
        ValidateAllProperties();

        foreach (var _ in new[] { this }.Where(item => item.HasErrors is false))
        {
            var request = new CreateConnectionRequest
            {
                Name = NewName,
                Ip = NewIp,
                Port = NewPort,
                GroupName = NormalizedGroupName(NewGroupName)
            };

            var created = await service.CreateConnectionAsync(request, owned, CancellationToken.None);
            notification.ShowResult(new OperationOutcome(created.IsSuccess, TextOf(created, "已新增")));

            foreach (var _2 in created.Successes)
            {
                NewName = "";
                NewIp = "";
                NewPort = 5900;
                NewGroupName = null;
                IsAddRowExpanded = false;
            }
        }
    }

    [RelayCommand]
    private async Task ApplyTestConnectionAsync(DashboardConnectionRow? row)
    {
        foreach (var target in new[] { row }.OfType<DashboardConnectionRow>())
        {
            await RunTestAsync(target, notifyOnSuccess: true);
        }
    }

    [RelayCommand]
    private async Task ApplyTestAllAsync()
    {
        foreach (var row in Connections.ToList())
        {
            await RunTestAsync(row, notifyOnSuccess: false);
        }
    }

    [RelayCommand]
    private async Task ApplyTestSelectedAsync()
    {
        foreach (var row in Connections.Where(item => item.IsSelected).ToList())
        {
            await RunTestAsync(row, notifyOnSuccess: false);
        }
    }

    [RelayCommand]
    private async Task ApplyDeleteConnectionAsync(DashboardConnectionRow? row)
    {
        foreach (var target in new[] { row }.OfType<DashboardConnectionRow>())
        {
            var confirmed = await confirmation.AskAsync($"確定要刪除「{target.Name}」嗎？此動作無法復原。");

            foreach (var _ in new[] { target }.Where(_ => confirmed))
            {
                var deleted = await service.DeleteConnectionAsync(target.Id, CancellationToken.None);
                notification.ShowResult(new OperationOutcome(deleted.IsSuccess, TextOf(deleted, "已刪除")));
            }
        }
    }

    [RelayCommand]
    private async Task ApplyDeleteSelectedAsync()
    {
        var selected = Connections.Where(row => row.IsSelected).ToList();

        foreach (var _ in new[] { selected }.Where(list => list.Count > 0))
        {
            var names = string.Join("、", selected.Select(row => row.Name));
            var confirmed = await confirmation.AskAsync(
                $"確定要刪除以下 {selected.Count} 筆連線嗎？此動作無法復原。\n{names}");

            foreach (var _2 in new[] { selected }.Where(_3 => confirmed))
            {
                await DeleteManyAsync(selected);
            }
        }
    }

    [RelayCommand]
    private void ApplyStartGroupRename(GroupRow? row)
    {
        foreach (var target in new[] { row }.OfType<GroupRow>())
        {
            target.EditName = target.Name;
            target.IsEditing = true;
        }
    }

    [RelayCommand]
    private void ApplyCancelGroupRename(GroupRow? row)
    {
        foreach (var target in new[] { row }.OfType<GroupRow>())
        {
            target.IsEditing = false;
        }
    }

    [RelayCommand]
    private async Task ApplySaveGroupRenameAsync(GroupRow? row)
    {
        var targets = new[] { row }.OfType<GroupRow>().Where(item => item.EditName.Trim().Length > 0);

        foreach (var target in targets)
        {
            var request = new RenameGroupRequest { OldName = target.Name, NewName = target.EditName.Trim() };
            var renamed = await service.RenameGroupAsync(request, CancellationToken.None);

            foreach (var _ in renamed.Successes)
            {
                target.IsEditing = false;
            }

            foreach (var message in renamed.Failures)
            {
                notification.ShowResult(new OperationOutcome(false, message));
            }
        }
    }

    [RelayCommand]
    private async Task ApplyUngroupAsync(GroupRow? row)
    {
        foreach (var target in new[] { row }.OfType<GroupRow>())
        {
            var confirmed = await confirmation.AskAsync(
                $"確定要取消群組「{target.Name}」嗎？此群組下的連線不會被刪除，只會變成未分組。");

            foreach (var _ in new[] { target }.Where(_2 => confirmed))
            {
                var ungrouped = await service.UngroupAsync(target.Name, CancellationToken.None);
                notification.ShowResult(new OperationOutcome(ungrouped.IsSuccess, TextOf(ungrouped, "已取消群組")));
            }
        }
    }

    [RelayCommand]
    private void ApplyToggleGroupSelection(CollectionViewGroup? group)
    {
        foreach (var target in new[] { group }.OfType<CollectionViewGroup>())
        {
            var rows = target.Items.OfType<DashboardConnectionRow>().ToList();
            var allSelected = rows.Count > 0 && rows.All(row => row.IsSelected);
            rows.ForEach(row => row.IsSelected = allSelected is false);
        }
    }

    [RelayCommand]
    private async Task ApplySubmitDeploySelectedAsync()
    {
        var selected = Connections.Where(row => row.IsSelected).ToList();

        foreach (var _ in new[] { selected }.Where(list => list.Count == 0))
        {
            notification.ShowResult(new OperationOutcome(false, "請先勾選至少一台部署目標"));
        }

        foreach (var _ in new[] { selected }.Where(list => list.Count > 0))
        {
            await SubmitDeployAsync(selected);
        }
    }

    [RelayCommand]
    private void ApplyRetryDeployItem(DashboardConnectionRow? row)
    {
        var targets = new[] { row }.OfType<DashboardConnectionRow>().Where(item => item.LastDeployTaskId is not null);

        foreach (var target in targets)
        {
            foreach (var task in DeployTasks.Where(item => item.TaskId == target.LastDeployTaskId))
            {
                Retry(task, target.Id);
            }
        }
    }

    [RelayCommand]
    private void ApplyRetryAllDeployFailed(DeployTaskRow? task)
    {
        foreach (var target in new[] { task }.OfType<DeployTaskRow>())
        {
            foreach (var connectionId in target.FailedConnectionIds)
            {
                Retry(target, connectionId);
            }
        }
    }

    [RelayCommand]
    private void ApplyCancelDeployTask(DeployTaskRow? task)
    {
        foreach (var target in new[] { task }.OfType<DeployTaskRow>())
        {
            service.CancelDeployTask(target.TaskId);
        }
    }

    #endregion

    #region 內部方法

    private ICollectionView CreateConnectionsView()
    {
        var view = CollectionViewSource.GetDefaultView(Connections);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(DashboardConnectionRow.GroupDisplayName)));
        view.SortDescriptions.Add(new SortDescription(nameof(DashboardConnectionRow.GroupDisplayName), ListSortDirection.Ascending));
        view.SortDescriptions.Add(new SortDescription(nameof(DashboardConnectionRow.Name), ListSortDirection.Ascending));

        var live = (ICollectionViewLiveShaping)view;
        live.IsLiveGrouping = true;
        live.IsLiveSorting = true;
        live.LiveGroupingProperties.Add(nameof(DashboardConnectionRow.GroupDisplayName));
        live.LiveSortingProperties.Add(nameof(DashboardConnectionRow.GroupDisplayName));
        live.LiveSortingProperties.Add(nameof(DashboardConnectionRow.Name));

        return view;
    }

    private async Task ApplyLoadDeployPreferencesAsync()
    {
        var preferences = await service.LoadDeployPreferencesAsync(CancellationToken.None);

        foreach (var value in preferences.Values)
        {
            PostToUi(() =>
            {
                DeployLocalSourcePath = value.LastLocalSourcePath;
                DeployRemoteTargetPath = value.LastRemoteTargetPath;
            });
        }
    }

    private async Task ApplySnapshotAsync()
    {
        var filter = new ConnectionFilter { Search = SearchQuery, OnlyAbnormal = ShowOnlyAbnormal };
        var rows = await service.LoadRowListAsync(filter, CancellationToken.None);
        var groups = await service.LoadGroupNameListAsync(CancellationToken.None);
        PostToUi(() => ApplyToUi(rows, groups));
    }

    private void ApplyToUi(
        Result<IReadOnlyList<DashboardRowSnapshot>> rows, Result<IReadOnlyList<string>> groups)
    {
        HasLoadError = rows.IsSuccess is false;
        LoadErrorMessage = rows.Failures.DefaultIfEmpty("").First();

        foreach (var names in groups.Values)
        {
            ApplyGroupsToUi(names);
        }

        foreach (var items in rows.Values)
        {
            ApplyConnectionsToUi(items);
        }
    }

    private void ApplyGroupsToUi(IReadOnlyList<string> names)
    {
        var incoming = names.ToHashSet();

        Groups.Where(name => incoming.Contains(name) is false).ToList()
            .ForEach(name => Groups.Remove(name));

        names.Where(name => Groups.Contains(name) is false).ToList()
            .ForEach(Groups.Add);

        GroupManagementRows.Where(row => incoming.Contains(row.Name) is false).ToList()
            .ForEach(row => GroupManagementRows.Remove(row));

        var existing = GroupManagementRows.Select(row => row.Name).ToHashSet();
        names.Where(name => existing.Contains(name) is false).ToList()
            .ForEach(name => GroupManagementRows.Add(new GroupRow { Name = name }));
    }

    private void ApplyConnectionsToUi(IReadOnlyList<DashboardRowSnapshot> items)
    {
        var incoming = items.Select(item => item.Connection.Id).ToHashSet();

        Connections.Where(row => incoming.Contains(row.Id) is false).ToList()
            .ForEach(RemoveRow);

        var existing = Connections.ToDictionary(row => row.Id);

        foreach (var item in items)
        {
            var matched = new[] { existing.GetValueOrDefault(item.Connection.Id) }.OfType<DashboardConnectionRow>().ToList();
            matched.ForEach(row => row.Snapshot = item);

            foreach (var _ in new[] { item }.Where(_2 => matched.Count == 0))
            {
                AddRow(item);
            }
        }

        HasNoConnections = Connections.Count == 0;
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
    }

    private void AddRow(DashboardRowSnapshot snapshot)
    {
        var row = new DashboardConnectionRow(snapshot);
        row.PropertyChanged += OnRowPropertyChanged;
        Connections.Add(row);
    }

    private void RemoveRow(DashboardConnectionRow row)
    {
        row.PropertyChanged -= OnRowPropertyChanged;
        Connections.Remove(row);
    }

    private async Task ApplyRecentEventsAsync(DashboardConnectionRow row)
    {
        var events = await service.LoadRecentEventListAsync(row.Id, CancellationToken.None);

        foreach (var items in events.Values)
        {
            PostToUi(() => row.ApplyRecentEvents(items));
        }
    }

    private async Task RunTestAsync(DashboardConnectionRow row, bool notifyOnSuccess)
    {
        row.IsTesting = true;
        using var release = new ScopeGuard(() => row.IsTesting = false);

        var tested = await service.TestConnectionAsync(row.Id, CancellationToken.None);
        var reachable = tested.Values.Any(snapshot => snapshot.Status == ConnectionStatus.Reachable);

        var message = tested.Values
            .Select(snapshot => snapshot.LastTestMessage)
            .DefaultIfEmpty(tested.Message)
            .First();

        foreach (var _ in new[] { row }.Where(_2 => notifyOnSuccess || reachable is false))
        {
            notification.ShowResult(new OperationOutcome(reachable, message));
        }
    }

    private async Task DeleteManyAsync(IReadOnlyList<DashboardConnectionRow> selected)
    {
        var outcomes = new List<Result>();

        foreach (var row in selected)
        {
            outcomes.Add(await service.DeleteConnectionAsync(row.Id, CancellationToken.None));
        }

        var succeeded = outcomes.Count(outcome => outcome.IsSuccess);
        notification.ShowResult(new OperationOutcome(
            succeeded == selected.Count, $"共 {selected.Count} 筆，成功刪除 {succeeded} 筆"));
    }

    private async Task SubmitDeployAsync(IReadOnlyList<DashboardConnectionRow> selected)
    {
        var parsed = DateTimeOffset.TryParse(DeployScheduledAtText, out var moment);
        var scheduleUsable = IsDeployScheduled is false || parsed;

        foreach (var _ in new[] { selected }.Where(_2 => scheduleUsable is false))
        {
            notification.ShowResult(new OperationOutcome(false, "排程時間格式錯誤，請輸入 yyyy-MM-dd HH:mm"));
        }

        foreach (var _ in new[] { selected }.Where(_2 => scheduleUsable))
        {
            var scheduledAt = new[] { moment }.Where(_3 => IsDeployScheduled).Cast<DateTimeOffset?>().FirstOrDefault();
            await SubmitDeployAsync(selected, scheduledAt);
        }
    }

    private async Task SubmitDeployAsync(IReadOnlyList<DashboardConnectionRow> selected, DateTimeOffset? scheduledAt)
    {
        var request = new DeploySubmission
        {
            SelectedTargetIds = selected.Select(row => row.Id).ToList(),
            LocalSourcePath = DeployLocalSourcePath,
            RemoteTargetPath = DeployRemoteTargetPath,
            ScheduledAt = scheduledAt
        };

        var submitted = await service.SubmitDeploymentAsync(request, CancellationToken.None);
        notification.ShowResult(new OperationOutcome(submitted.IsSuccess, TextOf(submitted, "已提交部署任務")));

        foreach (var submission in submitted.Values)
        {
            TrackTask(submission, selected, scheduledAt);
        }
    }

    private void TrackTask(
        DeploySubmissionResult submission, IReadOnlyList<DashboardConnectionRow> selected, DateTimeOffset? scheduledAt)
    {
        var initialState = new[] { DeployItemState.Scheduled }
            .Where(_ => scheduledAt is not null)
            .DefaultIfEmpty(DeployItemState.Pending)
            .First();

        var task = new DeployTaskRow(
            submission, DeployLocalSourcePath, DeployRemoteTargetPath, scheduledAt, DateTimeOffset.UtcNow, initialState);

        DeployTasks.Insert(0, task);
        TrimCompletedDeployTasks();

        var message = new[] { scheduledAt }
            .OfType<DateTimeOffset>()
            .Where(_ => initialState == DeployItemState.Scheduled)
            .Select(moment => $"排程於 {moment.ToLocalTime():MM-dd HH:mm} 派發")
            .DefaultIfEmpty("佇列中…")
            .First();

        foreach (var row in selected)
        {
            row.ApplyDeployProgress(new DeployItemProgress
            {
                TaskId = submission.TaskId,
                ConnectionId = row.Id,
                State = initialState,
                StateText = InitialStateText(initialState),
                Message = message
            });
        }

        IsDeployPanelExpanded = false;
    }

    private static string InitialStateText(DeployItemState state)
        => new[] { "排程中" }
            .Where(_ => state == DeployItemState.Scheduled)
            .DefaultIfEmpty("佇列中")
            .First();

    private void TrimCompletedDeployTasks()
        => DeployTasks
            .Where(task => task.AllTerminal)
            .Skip(Math.Max(0, MaxRetainedDeployTasks - DeployTasks.Count(task => task.AllTerminal is false)))
            .ToList()
            .ForEach(task => DeployTasks.Remove(task));

    private void Retry(DeployTaskRow task, int connectionId)
    {
        var retried = service.RetryDeployItem(new RetryItemRequest
        {
            TaskId = task.TaskId,
            ConnectionId = connectionId,
            LocalSourcePath = task.LocalSourcePath,
            RemoteTargetPath = task.RemoteTargetPath
        });

        foreach (var message in retried.Failures)
        {
            notification.ShowResult(new OperationOutcome(false, message));
        }
    }

    private static string? NormalizedGroupName(string? raw)
        => new[] { (raw ?? "").Trim() }.Where(name => name.Length > 0).FirstOrDefault();

    private static string TextOf(Result result, string successText)
        => new[] { successText }.Where(_ => result.IsSuccess).DefaultIfEmpty(result.Message).First();

    private static string TextOf<T>(Result<T> result, string successText)
        => new[] { successText }.Where(_ => result.IsSuccess).DefaultIfEmpty(result.Message).First();

    #endregion
}
