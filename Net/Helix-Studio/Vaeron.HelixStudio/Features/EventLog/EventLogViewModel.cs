using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public ObservableCollection<EventEntrySnapshot> Events { get; } = [];

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

    // 對齊 blueprint EventLogScreen.dc.html 的實際數值（26/36px），不是隨意抓的近似值。
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

    private void OnWorkspaceChanged()
    {
        // 依 blueprint：切換工作區時重置全部篩選狀態，避免殘留篩選造成誤判「沒有事件」。
        PostToUi(() =>
        {
            SearchText = "";
            SeverityFilter = "全部";
            TimeRangeFilter = "全部時間";
            ExpandedEvent = null;
        });
        RunSafe(ApplySnapshotAsync);
    }

    private async Task ApplySnapshotAsync()
    {
        var workspace = await workspaceFacade.LoadActiveWorkspaceAsync();
        var activeWorkspaceId = workspace?.Id;
        IReadOnlyList<EventEntrySnapshot> events = activeWorkspaceId is null
            ? []
            : await domain.LoadEventListAsync(activeWorkspaceId.Value);

        var settings = await domain.LoadLogSettingsAsync();
        var bannerText = settings.MinLevel == EventSeverity.None
            ? "目前記錄門檻設為「不記錄任何事件」，所有工具的事件都不會被記錄（套用到所有工作區，於設定 > 日誌調整）"
            : $"僅顯示等級 ≥「{SeverityLabel(settings.MinLevel)}」的事件，較低等級的事件不會被記錄（套用到所有工作區，於設定 > 日誌調整）";

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

        // 「今天」是滾動 24 小時視窗（now - eventTime <= 24h），不是行事曆日期比對——
        // 對齊 blueprint 的判準，避免午夜前後的邊界行為跟設計稿不一致。
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
        // 不用 try...catch——技術例外的唯一攔截點在 SharedKernel（viewmodels.md）；
        // 剪貼簿操作極少數情況下會因系統剪貼簿檢視鏈問題丟例外，交給 App 層級的
        // 全域未處理例外處理常式兜底，不在這裡另開例外處理路徑。
        // 複製內容比照 blueprint：指令、輸出、結束代碼三段都要有，不是只有 output——
        // 之前漏掉 command 跟 exitCode，複製出去的內容不完整。
        var content = $"$ {entry.Command}\n{entry.Output ?? entry.Message}\nexit code {entry.ExitCode}";
        System.Windows.Clipboard.SetText(content);
        CopyFeedback = "已複製";
        _copyFeedbackTimer.Stop();
        _copyFeedbackTimer.Start();
    }

    #endregion
}
