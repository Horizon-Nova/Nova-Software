using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vaeron.VNCSCP.Internal.Contracts;
using Vaeron.VNCSCP.Internal.Shell;
using Vaeron.VNCSCP.Internal.Wpf;

namespace Vaeron.VNCSCP.EventLog;

/// <summary>事件日誌畫面的繫結來源。三個過濾鍵原樣送進查詢，本端不解讀。</summary>
public sealed partial class EventLogViewModel(IEventLogFacade facade, INotificationService notification) : ViewModelBase
{
    public ObservableCollection<EventRow> Events { get; } = [];

    #region 控制項

    [ObservableProperty]
    private bool _hasNoEvents = true;

    [ObservableProperty]
    private bool _hasLoadError;

    [ObservableProperty]
    private string _loadErrorMessage = "";

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private string _selectedTimeRange = "All";

    [ObservableProperty]
    private string _selectedLevel = "All";

    #endregion

    #region 生命週期

    public void ApplyViewLoaded()
    {
        facade.Changed += OnEventsChanged;
        RunSafe(ApplySnapshotAsync);
    }

    public void ApplyViewUnloaded() => facade.Changed -= OnEventsChanged;

    private void OnEventsChanged() => RunSafe(ApplySnapshotAsync);

    #endregion

    #region 事件回呼

    partial void OnSearchQueryChanged(string value) => RunSafe(ApplySnapshotAsync);

    partial void OnSelectedTimeRangeChanged(string value) => RunSafe(ApplySnapshotAsync);

    partial void OnSelectedLevelChanged(string value) => RunSafe(ApplySnapshotAsync);

    #endregion

    #region 外部操作方法

    /// <summary>依當前條件匯出，結果走狀態列通知。</summary>
    public void ApplyExport(string path)
        => RunSafe(async () =>
        {
            var exported = await facade.ExportAsync(BuildFilter(), path, CancellationToken.None);
            var message = new[] { $"已匯出事件日誌：{path}" }
                .Where(_ => exported.IsSuccess)
                .DefaultIfEmpty(exported.Message)
                .First();

            notification.ShowResult(new OperationOutcome(exported.IsSuccess, message));
        });

    #endregion

    #region 命令

    [RelayCommand]
    private void ApplyReload() => RunSafe(ApplySnapshotAsync);

    [RelayCommand]
    private void ApplyToggleExpand(EventRow? row)
    {
        foreach (var target in new[] { row }.OfType<EventRow>().Where(item => item.HasDetail))
        {
            target.IsExpanded = target.IsExpanded is false;
        }
    }

    #endregion

    #region 內部方法

    private async Task ApplySnapshotAsync()
    {
        var loaded = await facade.LoadEventListAsync(BuildFilter(), CancellationToken.None);
        PostToUi(() => ApplyToUi(loaded));
    }

    private void ApplyToUi(Vaeron.Kernel.Result<IReadOnlyList<EventSnapshot>> loaded)
    {
        HasLoadError = loaded.IsSuccess is false;
        LoadErrorMessage = new[] { loaded.Message }.Where(_ => HasLoadError).DefaultIfEmpty("").First();

        foreach (var items in loaded.Values)
        {
            var expandedIds = Events.Where(row => row.IsExpanded).Select(row => row.Snapshot.Id).ToHashSet();
            Events.Clear();

            foreach (var item in items)
            {
                Events.Add(new EventRow { Snapshot = item, IsExpanded = expandedIds.Contains(item.Id) });
            }

            HasNoEvents = Events.Count == 0;
        }
    }

    private EventFilter BuildFilter() => new()
    {
        Search = SearchQuery,
        TimeRange = SelectedTimeRange,
        Level = SelectedLevel
    };

    #endregion
}
