using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vaeron.HelixStudio.Internal.Runtime;
using Vaeron.HelixStudio.Internal.Runtime.AppRuntime;
using Vaeron.HelixStudio.Internal.Runtime.Components;
using Vaeron.HelixStudio.WorkspaceManagement;

namespace Vaeron.HelixStudio.AppsManagement;

/// <summary>工具列顯示狀態（展開／閃爍／處理中）</summary>
public sealed record ToolRow(InstalledToolSnapshot Tool, bool IsExpanded, bool IsFlashed, bool IsPending);

public sealed partial class AppsManagementViewModel(
    IAppsManagementFacade appsManagement,
    IWorkspaceManagementFacade workspaceFacade,
    ConfirmationRuntime confirmationRuntime,
    NotificationRuntime notificationRuntime) : ViewModelBase
{
    private int? _activeWorkspaceId;
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    /// <summary>篩選 debounce 計時器（420ms）</summary>
    private readonly DispatcherTimer _filterDebounceTimer = new() { Interval = TimeSpan.FromMilliseconds(420) };

    /// <summary>操作成功列閃爍計時器（700ms）</summary>
    private readonly DispatcherTimer _flashTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };

    /// <summary>骨架屏最短可視時間</summary>
    private static readonly TimeSpan MinVisibleLoadingDuration = TimeSpan.FromMilliseconds(400);

    private IReadOnlyList<InstalledToolSnapshot> _rawTools = [];

    /// <summary>正在啟動/停止/移除中的工具 Id 集合</summary>
    private readonly HashSet<int> _pendingToolIds = [];

    public ObservableCollection<ToolRow> Tools { get; } = [];

    /// <summary>骨架屏欄位配置</summary>
    public static IReadOnlyList<TableSkeletonColumn> SkeletonColumns { get; } =
    [
        new() { Width = new GridLength(4) },
        new()
        {
            Width = new GridLength(66), Header = "狀態",
            Chips = [new TableSkeletonChip { Width = 46, Height = 15, Margin = new Thickness(8, 0, 0, 0) }],
        },
        new()
        {
            Width = new GridLength(1, GridUnitType.Star), Header = "名稱",
            Chips = [new TableSkeletonChip { Width = 140, Height = 12, Margin = new Thickness(8, 0, 12, 0) }],
        },
        new()
        {
            Width = new GridLength(58), Header = "CPU", HeaderMargin = new Thickness(0, 9, 0, 9),
            Chips = [new TableSkeletonChip { Width = 30, Height = 11, Margin = new Thickness(0) }],
        },
        new()
        {
            Width = new GridLength(66), Header = "記憶體", HeaderMargin = new Thickness(0, 9, 0, 9),
            Chips = [new TableSkeletonChip { Width = 40, Height = 11, Margin = new Thickness(0) }],
        },
        new()
        {
            Width = new GridLength(92), Header = "上次啟動", HeaderMargin = new Thickness(0, 9, 0, 9),
            Chips = [new TableSkeletonChip { Width = 56, Height = 11, Margin = new Thickness(0) }],
        },
        new()
        {
            Width = new GridLength(180), Header = "操作",
            Alignment = HorizontalAlignment.Right, HeaderMargin = new Thickness(0, 9, 12, 9),
            Chips =
            [
                new TableSkeletonChip { Width = 44, Height = 24, Margin = new Thickness(0, 0, 6, 0) },
                new TableSkeletonChip { Width = 44, Height = 24, Margin = new Thickness(0) },
            ],
        },
    ];

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private bool _showOnlyRunning;

    [ObservableProperty]
    private InstalledToolSnapshot? _expandedTool;

    [ObservableProperty]
    private string _expandedTab = "summary";

    [ObservableProperty]
    private bool _unlockPopupOpen;

    [ObservableProperty]
    private string _moduleKeyInput = "";

    /// <summary>解鎖金鑰錯誤時的行內錯誤訊息</summary>
    [ObservableProperty]
    private string? _unlockError;

    /// <summary>剛完成啟動/停止的列 Id（閃爍效果）</summary>
    [ObservableProperty]
    private int? _flashedToolId;

    /// <summary>首次載入中</summary>
    [ObservableProperty]
    private bool _isLoading = true;

    /// <summary>篩選 debounce 過渡狀態</summary>
    [ObservableProperty]
    private bool _filterLoading;

    /// <summary>是否顯示骨架屏</summary>
    public bool ShowSkeleton => IsLoading || FilterLoading;

    /// <summary>是否顯示空狀態</summary>
    public bool ShowEmptyState => !ShowSkeleton && Tools.Count == 0;

    partial void OnIsLoadingChanged(bool value) => RaiseSkeletonDerivedChanged();
    partial void OnFilterLoadingChanged(bool value) => RaiseSkeletonDerivedChanged();

    private void RaiseSkeletonDerivedChanged()
    {
        OnPropertyChanged(nameof(ShowSkeleton));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    #region 生命週期

    public void ApplyViewLoaded()
    {
        appsManagement.ToolsChanged += OnChanged;
        workspaceFacade.WorkspaceChanged += OnWorkspaceChanged;
        _refreshTimer.Tick += OnRefreshTick;
        _refreshTimer.Start();
        _filterDebounceTimer.Tick += OnFilterDebounceTick;
        _flashTimer.Tick += OnFlashTimerTick;
        RunSafe(ApplySnapshotAsync);
    }

    public void ApplyViewUnloaded()
    {
        appsManagement.ToolsChanged -= OnChanged;
        workspaceFacade.WorkspaceChanged -= OnWorkspaceChanged;
        _refreshTimer.Tick -= OnRefreshTick;
        _refreshTimer.Stop();
        _filterDebounceTimer.Tick -= OnFilterDebounceTick;
        _filterDebounceTimer.Stop();
        _flashTimer.Tick -= OnFlashTimerTick;
        _flashTimer.Stop();
    }

    private void OnChanged() => RunSafe(ApplySnapshotAsync);

    /// <summary>切換工作區骨架屏</summary>
    private void OnWorkspaceChanged() => RunSafe(ApplyReloadCoreAsync);

    /// <summary>1 秒輪詢重查（解鎖 Popup 開啟、篩選中、或有列展開時暫停）</summary>
    private void OnRefreshTick(object? sender, EventArgs e)
    {
        if (UnlockPopupOpen || FilterLoading || ExpandedTool is not null) return;
        RunSafe(ApplySnapshotAsync);
    }

    private void OnFilterDebounceTick(object? sender, EventArgs e)
    {
        _filterDebounceTimer.Stop();
        RunSafe(ApplySnapshotAsync);
    }

    private void OnFlashTimerTick(object? sender, EventArgs e)
    {
        _flashTimer.Stop();
        FlashedToolId = null;
        ApplyBuildRows();
    }

    private void ApplyScheduleFilter()
    {
        FilterLoading = true;
        _filterDebounceTimer.Stop();
        _filterDebounceTimer.Start();
    }

    private Task ApplySnapshotAsync() => ApplySnapshotCoreAsync(TimeSpan.Zero);

    private async Task ApplySnapshotCoreAsync(TimeSpan minimumVisibleDuration)
    {
        var stopwatch = Stopwatch.StartNew();

        var workspace = await workspaceFacade.LoadActiveWorkspaceAsync();
        _activeWorkspaceId = workspace?.Id;
        if (_activeWorkspaceId is null) return;

        var items = await appsManagement.LoadInstalledToolListAsync(_activeWorkspaceId.Value);

        IEnumerable<InstalledToolSnapshot> query = items;
        if (!string.IsNullOrWhiteSpace(SearchText))
            query = query.Where(t => t.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        if (ShowOnlyRunning)
            query = query.Where(t => t.IsRunning);

        var resultList = query.ToList();

        var remaining = minimumVisibleDuration - stopwatch.Elapsed;
        if (remaining > TimeSpan.Zero)
            await Task.Delay(remaining);

        PostToUi(() =>
        {
            _rawTools = resultList;

            if (ExpandedTool is not null)
                ExpandedTool = resultList.FirstOrDefault(t => t.Id == ExpandedTool.Id);

            ApplyBuildRows();

            IsLoading = false;
            FilterLoading = false;
        });
    }

    /// <summary>Tools 顯示列表組裝</summary>
    private void ApplyBuildRows()
    {
        Tools.Clear();
        foreach (var t in _rawTools)
        {
            var isExpanded = ExpandedTool is not null && ExpandedTool.Id == t.Id;
            var isFlashed = FlashedToolId == t.Id;
            var isPending = _pendingToolIds.Contains(t.Id);
            Tools.Add(new ToolRow(t, isExpanded, isFlashed, isPending));
        }

        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnSearchTextChanged(string value) => ApplyScheduleFilter();
    partial void OnShowOnlyRunningChanged(bool value) => ApplyScheduleFilter();

    #endregion

    #region 使用者操作入口

    [RelayCommand]
    private async Task ApplyToggleStartStopAsync(InstalledToolSnapshot tool)
    {
        if (tool.State == ToolState.Error)
        {
            notificationRuntime.ApplyShowFailure("需人工排除錯誤後才能重試");
            return;
        }

        _pendingToolIds.Add(tool.Id);
        ApplyBuildRows();
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = tool.IsRunning
                ? await appsManagement.StopToolAsync(tool.Id)
                : await appsManagement.StartToolAsync(tool.Id);
            var remaining = MinVisibleLoadingDuration - stopwatch.Elapsed;
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining);
            if (result.IsSuccess) notificationRuntime.ApplyShowSuccess(result.Message);
            else notificationRuntime.ApplyShowFailure(result.Message);
            if (result.IsSuccess)
            {
                FlashedToolId = tool.Id;
                _flashTimer.Stop();
                _flashTimer.Start();
            }
        }
        finally
        {
            _pendingToolIds.Remove(tool.Id);
            ApplyBuildRows();
        }
    }

    [RelayCommand]
    private async Task ApplyRemoveAsync(InstalledToolSnapshot tool)
    {
        var confirmed = await confirmationRuntime.AskAsync($"確定要移除「{tool.Name}」嗎？此動作無法復原。");
        if (!confirmed) return;

        _pendingToolIds.Add(tool.Id);
        ApplyBuildRows();
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await appsManagement.RemoveToolAsync(tool.Id);
            var remaining = MinVisibleLoadingDuration - stopwatch.Elapsed;
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining);
            if (result.IsSuccess) notificationRuntime.ApplyShowSuccess(result.Message);
            else notificationRuntime.ApplyShowFailure(result.Message);
        }
        finally
        {
            _pendingToolIds.Remove(tool.Id);
            ApplyBuildRows();
        }
    }

    [RelayCommand]
    private void ApplyToggleExpand(InstalledToolSnapshot tool)
    {
        var isSameTool = ExpandedTool is not null && ExpandedTool.Id == tool.Id;
        ExpandedTool = isSameTool ? null : tool;
        if (!isSameTool)
        {
            ExpandedTab = "summary";
            UnlockPopupOpen = false;
        }
        ApplyBuildRows();
    }

    [RelayCommand]
    private void ApplySelectTab(string tab) => ExpandedTab = tab;

    [RelayCommand]
    private void ApplyOpenUnlockPopup()
    {
        ModuleKeyInput = "";
        UnlockError = null;
        UnlockPopupOpen = true;
    }

    [RelayCommand]
    private void ApplyCloseUnlockPopup()
    {
        UnlockPopupOpen = false;
        ModuleKeyInput = "";
        UnlockError = null;
    }

    /// <summary>手動重新整理（顯示骨架屏）</summary>
    [RelayCommand]
    private Task ApplyReloadAsync() => ApplyReloadCoreAsync();

    private async Task ApplyReloadCoreAsync()
    {
        IsLoading = true;
        await ApplySnapshotCoreAsync(MinVisibleLoadingDuration);
    }

    [RelayCommand]
    private async Task ApplySetAutoStartAsync(InstalledToolSnapshot tool)
    {
        var result = await appsManagement.UpdateToolSettingsAsync(new UpdateToolSettingsRequest
        {
            Id = tool.Id,
            AutoStart = !tool.AutoStart,
            AutoRepair = tool.AutoRepair,
        });
        if (result.IsSuccess) notificationRuntime.ApplyShowSuccess(result.Message);
        else notificationRuntime.ApplyShowFailure(result.Message);
    }

    [RelayCommand]
    private async Task ApplySetAutoRepairAsync(InstalledToolSnapshot tool)
    {
        var result = await appsManagement.UpdateToolSettingsAsync(new UpdateToolSettingsRequest
        {
            Id = tool.Id,
            AutoStart = tool.AutoStart,
            AutoRepair = !tool.AutoRepair,
        });
        if (result.IsSuccess) notificationRuntime.ApplyShowSuccess(result.Message);
        else notificationRuntime.ApplyShowFailure(result.Message);
    }

    /// <summary>鎖定欄位點擊：未解鎖時開啟解鎖 Modal，否則切換</summary>
    [RelayCommand]
    private async Task ApplyAutoStartFieldClickAsync(InstalledToolSnapshot tool)
    {
        if (tool.IsAutoStartLocked) { ApplyOpenUnlockPopup(); return; }
        await ApplySetAutoStartAsync(tool);
    }

    [RelayCommand]
    private async Task ApplyAutoRepairFieldClickAsync(InstalledToolSnapshot tool)
    {
        if (tool.IsAutoRepairLocked) { ApplyOpenUnlockPopup(); return; }
        await ApplySetAutoRepairAsync(tool);
    }

    [RelayCommand]
    private async Task ApplyUnlockFieldsAsync(InstalledToolSnapshot tool)
    {
        var result = await appsManagement.UnlockFieldsAsync(new UnlockFieldsRequest
        {
            Id = tool.Id,
            ModuleKey = ModuleKeyInput,
        });
        if (result.IsSuccess)
        {
            notificationRuntime.ApplyShowSuccess(result.Message);
            ModuleKeyInput = "";
            UnlockError = null;
            UnlockPopupOpen = false;
        }
        else
        {
            UnlockError = "金鑰錯誤，請重新輸入";
        }
    }

    #endregion
}
