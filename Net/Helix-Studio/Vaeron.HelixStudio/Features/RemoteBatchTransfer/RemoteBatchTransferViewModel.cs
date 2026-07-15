using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vaeron.HelixStudio.Features.AppsManagement;
using Vaeron.HelixStudio.Features.RemoteBatchTransfer.Contracts;
using Vaeron.HelixStudio.Features.WorkspaceManagement;
using Vaeron.Platform;
using Vaeron.Platform.AppRuntime;

namespace Vaeron.HelixStudio.Features.RemoteBatchTransfer;

public sealed partial class RemoteBatchTransferViewModel(
    RemoteBatchTransferDomain domain,
    WorkspaceManagementFacade workspaceFacade,
    AppsManagementFacade appsManagement,
    ConfirmationRuntime confirmationRuntime,
    NotificationRuntime notificationRuntime) : ViewModelBase
{
    private const string ServiceAppName = "遠端批次傳檔";

    public ObservableCollection<SelectableConnection> Connections { get; } = [];
    public ObservableCollection<TransferTaskSnapshot> Tasks { get; } = [];
    public ObservableCollection<string> SourceBrowseEntries { get; } = [];

    /// <summary>連線清單搜尋用 ICollectionView 包住 Connections，不是另建一份過濾後的清單——
    /// 這樣勾選狀態（SelectableConnection 實例本身）在搜尋文字變動時不會被重建、不會遺失。
    /// 必須是 [ObservableProperty]，不能是純 CLR 屬性——ApplyViewLoaded() 才會真的指派值，
    /// View 建構當下（DataContext 剛設好那一刻）這裡還是 null，沒有變更通知的話 XAML 的
    /// ItemsSource 綁定會永遠停在最初評估到的 null，畫面上連線清單會整個是空的。</summary>
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
        domain.Changed += OnChanged;
        workspaceFacade.WorkspaceChanged += OnChanged;
        appsManagement.ToolsChanged += OnChanged;
        ConnectionsView = CollectionViewSource.GetDefaultView(Connections);
        ConnectionsView.Filter = o => o is SelectableConnection c &&
            (string.IsNullOrWhiteSpace(ConnectionSearchText) || c.Connection.Name.Contains(ConnectionSearchText, StringComparison.OrdinalIgnoreCase));
        _ = ApplySnapshotAsync();

        SourceBrowseEntries.Clear();
        foreach (var root in domain.LoadDriveRoots())
            SourceBrowseEntries.Add(root);
    }

    partial void OnConnectionSearchTextChanged(string value) => ConnectionsView.Refresh();

    public void ApplyViewUnloaded()
    {
        domain.Changed -= OnChanged;
        workspaceFacade.WorkspaceChanged -= OnChanged;
        appsManagement.ToolsChanged -= OnChanged;
        foreach (var c in Connections)
            c.PropertyChanged -= OnConnectionSelectionChanged;
    }

    private async void OnChanged() => await ApplySnapshotAsync();

    private async Task ApplySnapshotAsync()
    {
        var workspace = await workspaceFacade.LoadActiveWorkspaceAsync();
        IsServiceRunning = workspace is not null && await appsManagement.IsToolRunningByNameAsync(workspace.Id, ServiceAppName);

        var list = await domain.LoadConnectionListAsync();
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
            foreach (var t in domain.LoadSessionTasks())
                Tasks.Add(t);
        });
    }

    /// <summary>逐台連線的勾選/覆寫路徑是純 UI 狀態（SelectableConnection 自身的 ObservableProperty），
    /// 不會經過 domain.Changed 這條「重查快照」路徑——要讓「發布設定」面板與發布按鈕狀態即時反映
    /// 使用者剛剛的勾選動作，只能在這裡訂閱逐個項目的 PropertyChanged 自行轉拋。</summary>
    private void OnConnectionSelectionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
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
        var (success, message) = await domain.CreateConnectionAsync(new CreateConnectionRequest
        {
            Name = NewConnectionName,
            Host = NewConnectionHost,
            Port = NewConnectionPort,
            AuthMethod = NewConnectionAuthMethod,
            Account = NewConnectionAccount,
        });
        notificationRuntime.ApplyShowResult(success, message);
        if (success)
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
        var (success, message) = await domain.UpdateConnectionAsync(EditingConnection.Connection.Id, new CreateConnectionRequest
        {
            Name = EditConnectionName,
            Host = EditConnectionHost,
            Port = EditConnectionPort,
            AuthMethod = EditConnectionAuthMethod,
            Account = EditConnectionAccount,
        });
        notificationRuntime.ApplyShowResult(success, message);
        if (success) EditingConnection = null;
    }

    [RelayCommand]
    private async Task ApplyDeleteConnectionAsync(SelectableConnection item)
    {
        var confirmed = await confirmationRuntime.AskAsync($"確定要刪除連線「{item.Connection.Name}」嗎？此動作無法復原。");
        if (!confirmed) return;

        var (success, message) = await domain.DeleteConnectionAsync(item.Connection.Id);
        notificationRuntime.ApplyShowResult(success, message);
    }

    [RelayCommand]
    private async Task ApplyTestConnectionAsync(SelectableConnection item)
    {
        var (success, message) = await domain.TestConnectionAsync(item.Connection.Id);
        notificationRuntime.ApplyShowResult(success, message);
    }

    [RelayCommand]
    private async Task ApplyTestAllAsync()
    {
        foreach (var c in Connections)
            await domain.TestConnectionAsync(c.Connection.Id);
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
        SourcePath = string.IsNullOrEmpty(SourcePath) ? folderName : System.IO.Path.Combine(SourcePath, folderName);
        RefreshSourceBrowseEntries();
    }

    /// <summary>回到上一層目錄——對齊 blueprint 樹狀瀏覽器可以往上層重選的行為，
    /// 原本的「單層鑽入」只能一直往下，沒有回頭路。</summary>
    [RelayCommand]
    private void ApplyBrowseUp()
    {
        var parent = string.IsNullOrEmpty(SourcePath) ? null : System.IO.Path.GetDirectoryName(SourcePath);
        SourcePath = parent ?? "";
        RefreshSourceBrowseEntries();
    }

    private void RefreshSourceBrowseEntries()
    {
        SourceBrowseEntries.Clear();
        var entries = string.IsNullOrEmpty(SourcePath) ? domain.LoadDriveRoots() : domain.LoadSubdirectories(SourcePath);
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
            .Select(c => (c.Connection.Id, c.Connection.Name, c.DestPathOverride))
            .ToList();

        var task = await domain.DispatchTaskAsync(SelectedAction, SourcePath, DefaultDestPath, targets);
        var failedCount = task.Targets.Count(t => t.Status == Contracts.TransferTargetStatus.Failed);
        notificationRuntime.ApplyShowResult(failedCount == 0, $"已派送 {task.Targets.Count} 個目標，{failedCount} 個失敗");
        TaskLogOpen = true;
    }

    /// <summary>重試單一失敗目標——沿用該任務原本的來源/目的路徑與動作，只重新派送這一台。</summary>
    [RelayCommand]
    private async Task ApplyRetryTargetAsync((TransferTaskSnapshot task, TransferTargetSnapshot target) item)
    {
        var destOverride = item.target.DestPath == item.task.DefaultDestPath ? null : item.target.DestPath;
        var task = await domain.DispatchTaskAsync(item.task.Action, item.task.SourcePath, item.task.DefaultDestPath,
            [(item.target.ConnectionId, item.target.Name, destOverride)]);
        var failed = task.Targets.Count(t => t.Status == Contracts.TransferTargetStatus.Failed);
        notificationRuntime.ApplyShowResult(failed == 0, failed == 0 ? "重試成功" : "重試仍然失敗");
    }

    [RelayCommand]
    private async Task ApplyRetryAllFailedAsync(TransferTaskSnapshot task)
    {
        var failedTargets = task.Targets.Where(t => t.Status == Contracts.TransferTargetStatus.Failed).ToList();
        if (failedTargets.Count == 0) return;

        var targets = failedTargets
            .Select(t => (t.ConnectionId, t.Name, t.DestPath == task.DefaultDestPath ? null : t.DestPath))
            .ToList();
        var retried = await domain.DispatchTaskAsync(task.Action, task.SourcePath, task.DefaultDestPath, targets);
        var failedCount = retried.Targets.Count(t => t.Status == Contracts.TransferTargetStatus.Failed);
        notificationRuntime.ApplyShowResult(failedCount == 0, $"重試 {retried.Targets.Count} 個目標，{failedCount} 個仍然失敗");
    }

    #endregion
}
