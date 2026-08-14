using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vaeron.HelixStudio.Kernel;

namespace Vaeron.HelixStudio.SystemAssistant;

/// <summary>系統助理的畫面狀態與命令。</summary>
/// <remarks>
/// 只持有查詢條件與取樣時刻，每個動作都是改條件或對外執行後重新取一次投影。
/// 過濾、排序、值域對映、字串組裝全部在資料庫，這裡不做任何判斷。
/// </remarks>
public sealed partial class SystemAssistantViewModel(ISystemAssistantFacade facade, IEventSink events) : ObservableObject
{
    public ObservableCollection<ColumnView> Header { get; } = [];
    public ObservableCollection<SoftwareRowView> Rows { get; } = [];
    public ObservableCollection<FieldRowView> Fields { get; } = [];

    [ObservableProperty]
    private string filterKey = "all";

    [ObservableProperty]
    private string sortKey = "cpuPct";

    [ObservableProperty]
    private string sortDirection = "desc";

    [ObservableProperty]
    private string search = "";

    [ObservableProperty]
    private string activeId = "";

    [ObservableProperty]
    private string sampledAt = "—";

    [ObservableProperty]
    private string summary = "";

    [ObservableProperty]
    private string filterLabel = "";

    [ObservableProperty]
    private string nextFilterKey = "";

    [ObservableProperty]
    private bool filterOn;

    [ObservableProperty]
    private string activeName = "";

    [ObservableProperty]
    private string activeMeta = "";

    [ObservableProperty]
    private string activeToggleLabel = "";

    [ObservableProperty]
    private string activeToggleIcon = "";

    [ObservableProperty]
    private bool hasActive;

    [ObservableProperty]
    private int changedCount;

    [ObservableProperty]
    private bool hasChanges;

    partial void OnSearchChanged(string value) => Project();

    [RelayCommand]
    private void Load()
    {
        ActiveId = facade.Load(Query(), SampledAt).Rows.Select(row => row.Id).FirstOrDefault("");
        Project();
    }

    /// <summary>點表頭：欄位與方向都由該欄自己帶的 NextDirection 決定。</summary>
    [RelayCommand]
    private void Sort(ColumnView column)
    {
        SortKey = column.Key;
        SortDirection = column.NextDirection;
        Project();
    }

    [RelayCommand]
    private void SelectRow(string id)
    {
        ActiveId = id;
        Project();
    }

    /// <summary>對 Windows 執行該列綁定的動作。沒有動作可執行時不留事件。</summary>
    [RelayCommand]
    private void ToggleState(string id)
    {
        foreach (var change in facade.Toggle(id))
        {
            events.Write(change.Level, change.Text);
        }

        Project();
    }

    [RelayCommand]
    private void ToggleActive() => ToggleState(ActiveId);

    [RelayCommand]
    private void CycleFilter()
    {
        FilterKey = NextFilterKey;
        Project();
    }

    [RelayCommand]
    private void Rescan() => Project();

    [RelayCommand]
    private void RevertAll()
    {
        facade.Revert(ActiveId);
        Project();
    }

    [RelayCommand]
    private void ApplyAll()
    {
        var applied = ChangedCount;
        var name = ActiveName;

        facade.Apply(ActiveId);
        Project();

        foreach (var count in new[] { applied }.Where(value => value > 0))
        {
            events.Write("info", $"{name}：套用 {count} 項配額變更");
        }
    }

    /// <summary>採集器完成一次取樣：換上新的取樣時刻並重取投影。</summary>
    public void OnSampled(string stamp)
    {
        SampledAt = stamp;
        Project();
    }

    /// <summary>輸入框回寫草稿。</summary>
    public void EditField(string softwareId, string fieldKey, string value)
    {
        facade.SaveDraft(softwareId, fieldKey, value);
        Project();
    }

    private SystemAssistantQuery Query() => new(FilterKey, SortKey, SortDirection, Search, ActiveId);

    private void Project()
    {
        var snapshot = facade.Load(Query(), SampledAt);

        Fill(Header, snapshot.Columns);
        Fill(Rows, snapshot.Rows);
        Fill(Fields, snapshot.Fields);

        Summary = snapshot.Summary.Text;
        FilterLabel = snapshot.Summary.FilterLabel;
        NextFilterKey = snapshot.Summary.NextFilterKey;
        FilterOn = snapshot.Summary.FilterOn;
        ActiveName = snapshot.Summary.ActiveName;
        ActiveMeta = snapshot.Summary.ActiveMeta;
        ActiveToggleLabel = snapshot.Summary.ActiveToggleLabel;
        ActiveToggleIcon = snapshot.Summary.ActiveToggleIcon;
        HasActive = snapshot.Summary.HasActive;
        ChangedCount = snapshot.Summary.ChangedCount;
        HasChanges = snapshot.Summary.HasChanges;
    }

    private static void Fill<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}
