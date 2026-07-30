using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vaeron.HelixStudio.AppsManagement;
using Vaeron.HelixStudio.Internal.Runtime;
using Vaeron.HelixStudio.Internal.Runtime.AppRuntime;
using Vaeron.HelixStudio.WorkspaceManagement;

namespace Vaeron.HelixStudio.RemoteBatchTransfer;

public sealed partial class RemoteBatchTransferViewModel(
    IRemoteBatchTransferFacade remoteBatchTransfer,
    IWorkspaceManagementFacade workspaceFacade,
    IAppsManagementFacade appsManagement,
    ConfirmationRuntime confirmationRuntime,
    NotificationRuntime notificationRuntime) : ViewModelBase
{
    private const string ServiceAppName = "遠端批次傳檔";

    public ObservableCollection<SelectableConnection> Connections { get; } = [];
    public ObservableCollection<TransferTaskSnapshot> Tasks { get; } = [];
    public ObservableCollection<string> SourceBrowseEntries { get; } = [];

    /// <summary>連線清單搜尋用集合檢視</summary>
    [ObservableProperty] private ICollectionView _connectionsView = null!;

    [ObservableProperty] private bool _isServiceRunning;
    [ObservableProperty] private string _connectionSearchText = "";
    [ObservableProperty] private string _newConnectionName = "";
    [ObservableProperty] private string _newConnectionHost = "";
    [ObservableProperty] private int _newConnectionPort = 445;
    [ObservableProperty] private AuthMethod _newConnectionAuthMethod = AuthMethod.Password;
    [ObservableProperty] private string _newConnectionAccount = "";
    [ObservableProperty] private string _sourcePath = "";
    [ObservableProperty] private string _defaultDestPath = "";
    [ObservableProperty] private TransferAction _selectedAction = TransferAction.Copy;
    [ObservableProperty] private bool _taskLogOpen = true;

    [ObservableProperty] private SelectableConnection? _editingConnection;
    [ObservableProperty] private string _editConnectionName = "";
    [ObservableProperty] private string _editConnectionHost = "";
    [ObservableProperty] private int _editConnectionPort = 445;
    [ObservableProperty] private AuthMethod _editConnectionAuthMethod = AuthMethod.Password;
    [ObservableProperty] private string _editConnectionAccount = "";

    public bool HasSelectedTargets => Connections.Any(c => c.IsSelected);
    public bool IsCopySelected => SelectedAction == TransferAction.Copy;
    public bool IsMoveSelected => SelectedAction == TransferAction.Move;
    public int OverrideCount => Connections.Count(c => c.IsSelected && !string.IsNullOrWhiteSpace(c.DestPathOverride));

    public string DispatchLabel
    {
        get
        {
            if (!IsServiceRunning) return "服務未啟動";
            var selectedCount = Connections.Count(c => c.IsSelected);
            if (selectedCount == 0) return "請先選擇連線";
            if (string.IsNullOrWhiteSpace(DefaultDestPath) && Connections.Where(c => c.IsSelected).All(c => string.IsNullOrWhiteSpace(c.DestPathOverride)))
                return "請填寫目的路徑";
            return $"發布到 {selectedCount} 台電腦";
        }
    }

    public bool CanDispatch => IsServiceRunning && Connections.Any(c => c.IsSelected) &&
        (!string.IsNullOrWhiteSpace(DefaultDestPath) || Connections.Where(c => c.IsSelected).Any(c => !string.IsNullOrWhiteSpace(c.DestPathOverride)));

    partial void OnSelectedActionChanged(TransferAction value)
    {
        OnPropertyChanged(nameof(IsCopySelected));
        OnPropertyChanged(nameof(IsMoveSelected));
    }

    partial void OnIsServiceRunningChanged(bool value) => RaiseDispatchStateChanged();
    partial void OnDefaultDestPathChanged(string value) => RaiseDispatchStateChanged();

    private void RaiseDispatchStateChanged()
    {
        OnPropertyChanged(nameof(DispatchLabel));
        OnPropertyChanged(nameof(CanDispatch));
    }

    #region 生命週期

    public void ApplyViewLoaded()
    {
        remoteBatchTransfer.Changed += OnChanged;
        workspaceFacade.WorkspaceChanged += OnChanged;
        appsManagement.ToolsChanged += OnChanged;
        ConnectionsView = CollectionViewSource.GetDefaultView(Connections);
        ConnectionsView.Filter = o => o is SelectableConnection c &&
            (string.IsNullOrWhiteSpace(ConnectionSearchText) || c.Connection.Name.Contains(ConnectionSearchText, StringComparison.OrdinalIgnoreCase));
        _ = ApplySnapshotAsync();

        SourceBrowseEntries.Clear();
        foreach (var root in remoteBatchTransfer.LoadDriveRoots())
            SourceBrowseEntries.Add(root);
    }

    partial void OnConnectionSearchTextChanged(string value) => ConnectionsView.Refresh();

    public void ApplyViewUnloaded()
    {
        remoteBatchTransfer.Changed -= OnChanged;
        workspaceFacade.WorkspaceChanged -= OnChanged;
        appsManagement.ToolsChanged -= OnChanged;
        foreach (var c in Connections)
            c.PropertyChanged -= OnConnectionSelectionChanged;
    }

    private async void OnChanged() => await ApplySnapshotAsync();

    private async Task ApplySnapshotAsync()
    {
        var workspace = await workspaceFacade.LoadActiveWorkspaceAsync();
        IsServiceRunning = workspace is not null
            && await appsManagement.IsToolRunningByNameAsync(new IsToolRunningQuery(workspace.Id, ServiceAppName));

        var list = await remoteBatchTransfer.LoadConnectionListAsync();
        PostToUi(() =>
        {
            foreach (var c in Connections)
                c.PropertyChanged -= OnConnectionSelectionChanged;
            Connections.Clear();
            foreach (var c in list)
            {
                var selectable = new SelectableConnection(c);
                selectable.PropertyChanged += OnConnectionSelectionChanged;
                Connections.Add(selectable);
            }
            RaiseSelectionDependentState();

            Tasks.Clear();
            foreach (var t in remoteBatchTransfer.LoadSessionTasks())
                Tasks.Add(t);
        });
    }

    /// <summary>連線勾選/覆寫路徑變動時轉拋通知</summary>
    private void OnConnectionSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SelectableConnection.IsSelected) or nameof(SelectableConnection.DestPathOverride))
            RaiseSelectionDependentState();
    }

    private void RaiseSelectionDependentState()
    {
        OnPropertyChanged(nameof(HasSelectedTargets));
        OnPropertyChanged(nameof(OverrideCount));
        RaiseDispatchStateChanged();
    }

    #endregion

    #region 連線管理

    [RelayCommand]
    private async Task ApplyAddConnectionAsync()
    {
        var result = await remoteBatchTransfer.CreateConnectionAsync(new CreateConnectionRequest
        {
            Name = NewConnectionName,
            Host = NewConnectionHost,
            Port = NewConnectionPort,
            AuthMethod = NewConnectionAuthMethod,
            Account = NewConnectionAccount,
        });
        if (result.IsSuccess) notificationRuntime.ApplyShowSuccess(result.Message);
        else notificationRuntime.ApplyShowFailure(result.Message);
        if (result.IsSuccess)
        {
            NewConnectionName = "";
            NewConnectionHost = "";
            NewConnectionAccount = "";
            NewConnectionAuthMethod = AuthMethod.Password;
        }
    }

    [RelayCommand]
    private void ApplyStartEditConnection(SelectableConnection item)
    {
        EditingConnection = item;
        EditConnectionName = item.Connection.Name;
        EditConnectionHost = item.Connection.Host;
        EditConnectionPort = item.Connection.Port;
        EditConnectionAuthMethod = item.Connection.AuthMethod;
        EditConnectionAccount = item.Connection.Account;
    }

    [RelayCommand]
    private void ApplyCancelEditConnection() => EditingConnection = null;

    [RelayCommand]
    private async Task ApplySaveEditConnectionAsync()
    {
        if (EditingConnection is null) return;
        var result = await remoteBatchTransfer.UpdateConnectionAsync(new UpdateConnectionRequest(
            EditingConnection.Connection.Id,
            new CreateConnectionRequest
            {
                Name = EditConnectionName,
                Host = EditConnectionHost,
                Port = EditConnectionPort,
                AuthMethod = EditConnectionAuthMethod,
                Account = EditConnectionAccount,
            }));
        if (result.IsSuccess) notificationRuntime.ApplyShowSuccess(result.Message);
        else notificationRuntime.ApplyShowFailure(result.Message);
        if (result.IsSuccess) EditingConnection = null;
    }

    [RelayCommand]
    private async Task ApplyDeleteConnectionAsync(SelectableConnection item)
    {
        var confirmed = await confirmationRuntime.AskAsync($"確定要刪除連線「{item.Connection.Name}」嗎？此動作無法復原。");
        if (!confirmed) return;

        var result = await remoteBatchTransfer.DeleteConnectionAsync(item.Connection.Id);
        if (result.IsSuccess) notificationRuntime.ApplyShowSuccess(result.Message);
        else notificationRuntime.ApplyShowFailure(result.Message);
    }

    [RelayCommand]
    private async Task ApplyTestConnectionAsync(SelectableConnection item)
    {
        var result = await remoteBatchTransfer.TestConnectionAsync(item.Connection.Id);
        if (result.IsSuccess) notificationRuntime.ApplyShowSuccess(result.Message);
        else notificationRuntime.ApplyShowFailure(result.Message);
    }

    [RelayCommand]
    private async Task ApplyTestAllAsync()
    {
        foreach (var c in Connections)
            await remoteBatchTransfer.TestConnectionAsync(c.Connection.Id);
    }

    [RelayCommand]
    private void ApplySelectAllConnected()
    {
        foreach (var c in Connections.Where(c => c.Connection.Status == ConnectionStatus.Connected))
            c.IsSelected = true;
    }

    [RelayCommand]
    private void ApplyClearSelection()
    {
        foreach (var c in Connections)
            c.IsSelected = false;
    }

    #endregion

    #region 發布

    [RelayCommand]
    private void ApplySelectAction(TransferAction action) => SelectedAction = action;

    [RelayCommand]
    private void ApplyBrowseInto(string folderName)
    {
        SourcePath = string.IsNullOrEmpty(SourcePath) ? folderName : Path.Combine(SourcePath, folderName);
        RefreshSourceBrowseEntries();
    }

    /// <summary>回到上一層目錄</summary>
    [RelayCommand]
    private void ApplyBrowseUp()
    {
        var parent = string.IsNullOrEmpty(SourcePath) ? null : Path.GetDirectoryName(SourcePath);
        SourcePath = parent ?? "";
        RefreshSourceBrowseEntries();
    }

    private void RefreshSourceBrowseEntries()
    {
        SourceBrowseEntries.Clear();
        var entries = string.IsNullOrEmpty(SourcePath) ? remoteBatchTransfer.LoadDriveRoots() : remoteBatchTransfer.LoadSubdirectories(SourcePath);
        foreach (var entry in entries)
            SourceBrowseEntries.Add(entry);
    }

    [RelayCommand]
    private void ApplyResetOverride(SelectableConnection item) => item.DestPathOverride = null;

    [RelayCommand]
    private void ApplyToggleTaskLog() => TaskLogOpen = !TaskLogOpen;

    [RelayCommand]
    private async Task ApplyDispatchAsync()
    {
        var selected = Connections.Where(c => c.IsSelected).ToList();
        if (selected.Count == 0) return;

        var targets = selected
            .Select(c => new DispatchTarget(c.Connection.Id, c.Connection.Name, c.DestPathOverride))
            .ToList();

        var task = await remoteBatchTransfer.DispatchTaskAsync(new DispatchTaskRequest(SelectedAction, SourcePath, DefaultDestPath, targets));
        var failedCount = task.Targets.Count(t => t.Status == TransferTargetStatus.Failed);
        var summary = $"已派送 {task.Targets.Count} 個目標，{failedCount} 個失敗";
        if (failedCount == 0) notificationRuntime.ApplyShowSuccess(summary);
        else notificationRuntime.ApplyShowFailure(summary);
        TaskLogOpen = true;
    }

    /// <summary>重試單一失敗目標</summary>
    [RelayCommand]
    private async Task ApplyRetryTargetAsync(TaskTargetPair item)
    {
        var destOverride = item.Target.DestPath == item.Task.DefaultDestPath ? null : item.Target.DestPath;
        var task = await remoteBatchTransfer.DispatchTaskAsync(new DispatchTaskRequest(
            item.Task.Action, item.Task.SourcePath, item.Task.DefaultDestPath,
            [new DispatchTarget(item.Target.ConnectionId, item.Target.Name, destOverride)]));
        var failed = task.Targets.Count(t => t.Status == TransferTargetStatus.Failed);
        if (failed == 0) notificationRuntime.ApplyShowSuccess("重試成功");
        else notificationRuntime.ApplyShowFailure("重試仍然失敗");
    }

    [RelayCommand]
    private async Task ApplyRetryAllFailedAsync(TransferTaskSnapshot task)
    {
        var failedTargets = task.Targets.Where(t => t.Status == TransferTargetStatus.Failed).ToList();
        if (failedTargets.Count == 0) return;

        var targets = failedTargets
            .Select(t => new DispatchTarget(t.ConnectionId, t.Name, t.DestPath == task.DefaultDestPath ? null : t.DestPath))
            .ToList();
        var retried = await remoteBatchTransfer.DispatchTaskAsync(new DispatchTaskRequest(task.Action, task.SourcePath, task.DefaultDestPath, targets));
        var failedCount = retried.Targets.Count(t => t.Status == TransferTargetStatus.Failed);
        var summary = $"重試 {retried.Targets.Count} 個目標，{failedCount} 個仍然失敗";
        if (failedCount == 0) notificationRuntime.ApplyShowSuccess(summary);
        else notificationRuntime.ApplyShowFailure(summary);
    }

    #endregion
}
