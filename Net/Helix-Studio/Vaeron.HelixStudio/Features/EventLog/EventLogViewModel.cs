using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vaeron.HelixStudio.Components;
using Vaeron.HelixStudio.Features.EventLog.Contracts;
using Vaeron.HelixStudio.Features.Settings;
using Vaeron.HelixStudio.Features.Settings.Contracts;
using Vaeron.HelixStudio.Features.WorkspaceManagement;
using Vaeron.Platform;

namespace Vaeron.HelixStudio.Features.EventLog;

public sealed partial class EventLogViewModel(
    EventLogDomain domain,
    WorkspaceManagementFacade workspaceFacade,
    SettingsFacade settingsFacade) : ViewModelBase
{
    private IReadOnlyList<EventEntrySnapshot> _allEvents = [];
    private int? _activeWorkspaceId;

    /// <summary>骨架屏最短可視時間</summary>
    private static readonly TimeSpan MinVisibleLoadingDuration = TimeSpan.FromMilliseconds(400);

    public ObservableCollection<EventEntrySnapshot> Events { get; } = [];

    /// <summary>骨架屏欄位配置</summary>
    public static IReadOnlyList<TableSkeletonColumn> SkeletonColumns { get; } =
    [
        new() { Width = new GridLength(4) },
        new()
        {
            Width = new GridLength(60), Header = "等級",
            Chips = [new TableSkeletonChip { Width = 44, Height = 14, CornerRadius = 7, Margin = new Thickness(8, 0, 0, 0) }],
        },
        new()
        {
            Width = new GridLength(140), Header = "工具",
            Chips = [new TableSkeletonChip { Width = 98, Height = 11, Margin = new Thickness(8, 0, 12, 0) }],
        },
        new()
        {
            Width = new GridLength(1, GridUnitType.Star), Header = "訊息",
            Chips = [new TableSkeletonChip { Width = 178, Height = 11, Margin = new Thickness(8, 0, 12, 0) }],
        },
        new()
        {
            Width = new GridLength(140), Header = "時間",
            Chips = [new TableSkeletonChip { Width = 84, Height = 11, Margin = new Thickness(8, 0, 0, 0) }],
        },
        new() { Width = new GridLength(32) },
    ];

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _severityFilter = "全部";

    [ObservableProperty]
    private string _timeRangeFilter = "全部時間";

    [ObservableProperty]
    private EventEntrySnapshot? _expandedEvent;

    [ObservableProperty]
    private string _minLevelBannerText = "";

    [ObservableProperty]
    private string _copyFeedback = "";

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private double _rowHeight = ComfortableRowHeight;

    private const double ComfortableRowHeight = 36;
    private const double CompactRowHeight = 26;

    private readonly System.Windows.Threading.DispatcherTimer _copyFeedbackTimer =
        new() { Interval = TimeSpan.FromSeconds(1.5) };

    public string[] SeverityOptions { get; } = ["全部", "一般", "警告", "錯誤"];
    public string[] TimeRangeOptions { get; } = ["全部時間", "今天", "最近 7 天", "最近 30 天"];

    #region 生命週期

    public void ApplyViewLoaded()
    {
        domain.Changed += OnChanged;
        workspaceFacade.WorkspaceChanged += OnWorkspaceChanged;
        settingsFacade.SettingsChanged += OnSettingsChanged;
        _copyFeedbackTimer.Tick += OnCopyFeedbackTimerTick;
        RunSafe(ApplyDensityAsync);
        RunSafe(ApplySnapshotAsync);
    }

    public void ApplyViewUnloaded()
    {
        domain.Changed -= OnChanged;
        workspaceFacade.WorkspaceChanged -= OnWorkspaceChanged;
        settingsFacade.SettingsChanged -= OnSettingsChanged;
        _copyFeedbackTimer.Tick -= OnCopyFeedbackTimerTick;
        _copyFeedbackTimer.Stop();
    }

    private void OnCopyFeedbackTimerTick(object? sender, EventArgs e)
    {
        _copyFeedbackTimer.Stop();
        CopyFeedback = "";
    }

    private void OnSettingsChanged() => RunSafe(ApplyDensityAsync);

    private async Task ApplyDensityAsync()
    {
        var settings = await settingsFacade.LoadSettingsAsync();
        PostToUi(() => RowHeight = settings.DataDensity == DataDensity.Compact ? CompactRowHeight : ComfortableRowHeight);
    }

    private void OnChanged() => RunSafe(ApplySnapshotAsync);

    /// <summary>切換工作區骨架屏</summary>
    private void OnWorkspaceChanged()
    {
        PostToUi(() =>
        {
            SearchText = "";
            SeverityFilter = "全部";
            TimeRangeFilter = "全部時間";
            ExpandedEvent = null;
            IsLoading = true;
        });
        RunSafe(() => ApplySnapshotCoreAsync(MinVisibleLoadingDuration));
    }

    private Task ApplySnapshotAsync() => ApplySnapshotCoreAsync(TimeSpan.Zero);

    private async Task ApplySnapshotCoreAsync(TimeSpan minimumVisibleDuration)
    {
        var stopwatch = Stopwatch.StartNew();

        var workspace = await workspaceFacade.LoadActiveWorkspaceAsync();
        var activeWorkspaceId = workspace?.Id;
        IReadOnlyList<EventEntrySnapshot> events = activeWorkspaceId is null
            ? []
            : await domain.LoadEventListAsync(activeWorkspaceId.Value);

        var settings = await domain.LoadLogSettingsAsync();
        var bannerText = settings.MinLevel == EventSeverity.None
            ? "目前記錄門檻設為「不記錄任何事件」，所有工具的事件都不會被記錄（套用到所有工作區，於設定 > 日誌調整）"
            : $"僅顯示等級 ≥「{SeverityLabel(settings.MinLevel)}」的事件，較低等級的事件不會被記錄（套用到所有工作區，於設定 > 日誌調整）";

        var remaining = minimumVisibleDuration - stopwatch.Elapsed;
        if (remaining > TimeSpan.Zero)
            await Task.Delay(remaining);

        PostToUi(() =>
        {
            _activeWorkspaceId = activeWorkspaceId;
            _allEvents = events;
            MinLevelBannerText = bannerText;
            IsLoading = false;
            ApplyFilter();
        });
    }

    #endregion

    #region 篩選

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSeverityFilterChanged(string value) => ApplyFilter();
    partial void OnTimeRangeFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var now = DateTimeOffset.UtcNow;
        IEnumerable<EventEntrySnapshot> query = _allEvents;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(e =>
                e.ToolName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                e.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        query = SeverityFilter switch
        {
            "一般" => query.Where(e => e.Severity == EventSeverity.Info),
            "警告" => query.Where(e => e.Severity == EventSeverity.Warning),
            "錯誤" => query.Where(e => e.Severity == EventSeverity.Error),
            _ => query,
        };

        query = TimeRangeFilter switch
        {
            "今天" => query.Where(e => now - e.Timestamp <= TimeSpan.FromHours(24)),
            "最近 7 天" => query.Where(e => e.Timestamp >= now.AddDays(-7)),
            "最近 30 天" => query.Where(e => e.Timestamp >= now.AddDays(-30)),
            _ => query,
        };

        Events.Clear();
        foreach (var e in query)
            Events.Add(e);
    }

    private static string SeverityLabel(EventSeverity severity) => severity switch
    {
        EventSeverity.Info => "一般",
        EventSeverity.Warning => "警告",
        EventSeverity.Error => "錯誤",
        EventSeverity.None => "無（不記錄任何事件）",
        _ => severity.ToString(),
    };

    #endregion

    #region 使用者操作入口

    [RelayCommand]
    private void ApplySetSeverityFilter(string severity) => SeverityFilter = severity;

    [RelayCommand]
    private void ApplySetTimeRangeFilter(string range) => TimeRangeFilter = range;

    [RelayCommand]
    private void ApplyToggleExpand(EventEntrySnapshot entry)
        => ExpandedEvent = ReferenceEquals(ExpandedEvent, entry) ? null : entry;

    [RelayCommand]
    private void ApplyCopyOutput(EventEntrySnapshot entry)
    {
        var content = $"$ {entry.Command}\n{entry.Output ?? entry.Message}\nexit code {entry.ExitCode}";
        System.Windows.Clipboard.SetText(content);
        CopyFeedback = "已複製";
        _copyFeedbackTimer.Stop();
        _copyFeedbackTimer.Start();
    }

    #endregion
}
