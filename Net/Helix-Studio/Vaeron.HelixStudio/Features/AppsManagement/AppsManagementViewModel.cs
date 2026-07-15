using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vaeron.HelixStudio.Features.AppsManagement.Contracts;
using Vaeron.HelixStudio.Features.WorkspaceManagement;
using Vaeron.Platform;
using Vaeron.Platform.AppRuntime;

namespace Vaeron.HelixStudio.Features.AppsManagement;

/// <summary>畫面用的工具列，附帶「這一列目前算不算展開／算不算剛操作完閃爍／算不算資料
/// 變動中」這幾個純 UI 呈現狀態——跟 SettingsViewModel 的 WorkspaceRow 同一個理由：這些狀態
/// 每一列都要問，若不在建立列表當下算好，XAML 端就得靠 MultiBinding 比對 Id 跟
/// ExpandedTool/FlashedToolId/_pendingToolIds 湊出答案，等於把同一段「這是不是同一列」的
/// 判斷邏輯，散落複製進好幾個 Converter 裡（之前 ToolIsExpandedConverter 跟新加的
/// flash/樣式判斷各自比對一次，正是這種散落式判斷該避免的樣子）。單一定義處在
/// ApplyBuildRows，其餘地方只消費算好的布林值。</summary>
public sealed record ToolRow(InstalledToolSnapshot Tool, bool IsExpanded, bool IsFlashed, bool IsPending);

public sealed partial class AppsManagementViewModel(
    AppsManagementDomain domain,
    WorkspaceManagementFacade workspaceFacade,
    ConfirmationRuntime confirmationRuntime,
    NotificationRuntime notificationRuntime) : ViewModelBase
{
    private int? _activeWorkspaceId;
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    /// <summary>對齊 blueprint 的 _scheduleFilterApply：搜尋框打字／只顯示執行中切換後，
    /// 420ms 內沒有再變動才真的重查，避免每個按鍵都整批重建 Tools 集合。</summary>
    private readonly DispatcherTimer _filterDebounceTimer = new() { Interval = TimeSpan.FromMilliseconds(420) };

    /// <summary>對齊 blueprint 的 flashName：啟動/停止成功後，該列背景短暫高亮 700ms 再淡出，
    /// 讓使用者看得到「剛剛那個操作生效了」，不是靠使用者自己去對照狀態文字有沒有變。</summary>
    private readonly DispatcherTimer _flashTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };

    /// <summary>骨架動畫的最短可視時間——本機 SQLite 查詢常常在一個 WPF 畫面更新週期內就
    /// 跑完，IsLoading/IsPending 若只單純「跟著實際耗時開關」，骨架屏會在使用者眼睛還沒
    /// 反應過來前就消失，等於使用者點了重新整理／啟動／移除卻感覺畫面「什麼都沒發生」。
    /// 對齊 blueprint 對 toggle/remove 用 wait(550ms)/wait(450ms) 保證使用者看得到操作生效
    /// 的同一個理由，這裡統一訂一個門檻，不是各操作各自决定一個數字。</summary>
    private static readonly TimeSpan MinVisibleLoadingDuration = TimeSpan.FromMilliseconds(400);

    private IReadOnlyList<InstalledToolSnapshot> _rawTools = [];

    /// <summary>正在進行啟動/停止/移除的工具 Id 集合——對齊 blueprint 的 pendingNames。只要
    /// 涉及資料變動的操作都統一走這個集合＋列級骨架動畫，不是每種操作各自發明一套視覺。</summary>
    private readonly HashSet<int> _pendingToolIds = [];

    public ObservableCollection<ToolRow> Tools { get; } = [];

    /// <summary>骨架屏佔位列數量——對齊 blueprint 的 appsSkeletonItems: [1,2,3,4,5]，
    /// 固定 5 筆，View 端純綁定不需要知道數字從哪來。</summary>
    public static IReadOnlyList<int> SkeletonPlaceholders { get; } = [1, 2, 3, 4, 5];

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

    /// <summary>金鑰錯誤時的行內錯誤訊息——對齊 blueprint keyModal 的 keyError，跟
    /// NotificationRuntime 的 Toast 不衝突，兩者一起顯示（Toast 是全站慣例，行內錯誤是
    /// 這個 Modal 特有、指出「就是這個欄位錯了」）。</summary>
    [ObservableProperty]
    private string? _unlockError;

    /// <summary>剛完成啟動/停止的那一列 Id，畫面用來套用 flash 背景，700ms 後由 _flashTimer 歸零。</summary>
    [ObservableProperty]
    private int? _flashedToolId;

    /// <summary>對齊 blueprint 的 appsLoading——第一次載入完成前是 true，「沒有符合條件的工具」
    /// 這句空狀態文字只在「資料已經載入過、篩選後確實是零筆」才該出現，不能在還沒讀到資料
    /// 的當下也顯示，那樣使用者會誤以為真的沒有工具。</summary>
    [ObservableProperty]
    private bool _isLoading = true;

    /// <summary>篩選條件變動後、debounce 還沒settle 前的過渡狀態——對齊 blueprint 的
    /// filterLoading，這段期間畫面顯示骨架屏而不是舊資料，避免使用者以為篩選已經套用。</summary>
    [ObservableProperty]
    private bool _filterLoading;

    /// <summary>畫面該不該顯示骨架屏——首次載入或篩選 debounce 期間都算，兩種情況共用同一套
    /// 骨架列版面，不需要 View 端另外組合判斷。</summary>
    public bool ShowSkeleton => IsLoading || FilterLoading;

    /// <summary>空狀態文字要不要顯示，合一判斷「已經載入過、篩選也已 settle」跟「確實零筆」
    /// 兩個條件，View 端不用自己再組 MultiBinding。</summary>
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
        domain.Changed += OnChanged;
        workspaceFacade.WorkspaceChanged += OnWorkspaceChanged;
        _refreshTimer.Tick += OnRefreshTick;
        _refreshTimer.Start();
        _filterDebounceTimer.Tick += OnFilterDebounceTick;
        _flashTimer.Tick += OnFlashTimerTick;
        RunSafe(ApplySnapshotAsync);
    }

    public void ApplyViewUnloaded()
    {
        domain.Changed -= OnChanged;
        workspaceFacade.WorkspaceChanged -= OnWorkspaceChanged;
        _refreshTimer.Tick -= OnRefreshTick;
        _refreshTimer.Stop();
        _filterDebounceTimer.Tick -= OnFilterDebounceTick;
        _filterDebounceTimer.Stop();
        _flashTimer.Tick -= OnFlashTimerTick;
        _flashTimer.Stop();
    }

    private void OnChanged() => RunSafe(ApplySnapshotAsync);
    private void OnWorkspaceChanged() => RunSafe(ApplySnapshotAsync);
    /// <summary>解鎖 Popup 開啟期間跳過這次 1 秒輪詢重查——ApplySnapshotAsync 目前是整批
    /// Clear／重新 Add（viewmodels.md 預設做法），ItemsControl 因此每次都會整批重建容器，
    /// 使用者正在 Popup 裡打字時，輸入框跟著整批重建會被打斷、失焦。折衷做法：Popup 開著時
    /// 暫停輪詢，不強行改寫成差異更新（差異更新需要額外的全量校正機制，這裡問題本身很小，
    /// 犧牲「Popup 開著時 CPU/記憶體暫時不更新」換取「打字不被打斷」，不需要為此擴大改動範圍）。</summary>
    private void OnRefreshTick(object? sender, EventArgs e)
    {
        if (UnlockPopupOpen || FilterLoading) return;
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

    /// <summary>RunSafe(ApplySnapshotAsync) 這種方法群組轉型只接受零參數的 Func&lt;Task&gt;，
    /// 所以「不保證最短可視時間」的預設路徑維持零參數簽章，需要保證的呼叫端（手動重新整理）
    /// 改叫下面帶參數的 ApplySnapshotCoreAsync，兩者共用同一份查詢邏輯，不是各自重寫一次。</summary>
    private Task ApplySnapshotAsync() => ApplySnapshotCoreAsync(TimeSpan.Zero);

    private async Task ApplySnapshotCoreAsync(TimeSpan minimumVisibleDuration)
    {
        var stopwatch = Stopwatch.StartNew();

        var workspace = await workspaceFacade.LoadActiveWorkspaceAsync();
        _activeWorkspaceId = workspace?.Id;
        if (_activeWorkspaceId is null) return;

        var items = await domain.LoadInstalledToolListAsync(_activeWorkspaceId.Value);

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

            // ExpandedTool 指向的是重查前的舊實例——每次重查都要用同一個 Id 從新結果裡
            // 重新對應，倒數計時（UnlockExpiresAt）等資訊才會隨著 1 秒刷新真的更新；
            // 對應不到（工具被移除）時視同收合展開面板，不留著指向已不存在資料的參考。
            if (ExpandedTool is not null)
                ExpandedTool = resultList.FirstOrDefault(t => t.Id == ExpandedTool.Id);

            ApplyBuildRows();

            IsLoading = false;
            FilterLoading = false;
        });
    }

    /// <summary>Tools 顯示列表的唯一組裝點——把 _rawTools（純資料）套上「是否展開／是否
    /// 剛閃爍過」這兩個 UI 狀態，任何會影響這兩個狀態的操作（重查、展開/收合、閃爍計時）
    /// 都呼叫這裡重建，不各自在呼叫端拼湊。</summary>
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

        // ShowEmptyState 衍生自 Tools.Count，但 ObservableCollection 的 CollectionChanged
        // 不會自動觸發「ShowEmptyState 變了」這個屬性變更通知——每次改動 Tools 都要在這裡
        // 手動補一次，否則畫面會停留在重建前的空狀態判斷，跟 IsLoading/FilterLoading 那兩個
        // 已經有 partial 方法補的來源不是同一件事，不能只靠 RaiseSkeletonDerivedChanged。
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
            notificationRuntime.ApplyShowResult(false, "需人工排除錯誤後才能重試");
            return;
        }

        _pendingToolIds.Add(tool.Id);
        ApplyBuildRows();
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var (success, message) = tool.IsRunning
                ? await domain.StopToolAsync(tool.Id)
                : await domain.StartToolAsync(tool.Id);
            var remaining = MinVisibleLoadingDuration - stopwatch.Elapsed;
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining);
            notificationRuntime.ApplyShowResult(success, message);
            if (success)
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
            var (success, message) = await domain.RemoveToolAsync(tool.Id);
            var remaining = MinVisibleLoadingDuration - stopwatch.Elapsed;
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining);
            notificationRuntime.ApplyShowResult(success, message);
        }
        finally
        {
            // 移除成功的話這個 Id 已經不在下次重查的 _rawTools 裡，ApplyBuildRows 不會再產生
            // 這一列；移除失敗則這裡把列還原成正常樣子，不留在骨架屏卡住。
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

    /// <summary>對齊按鈕的 ToolTip 承諾（「重新載入資料，會重新顯示載入骨架屏」）與 blueprint
    /// 的 reloadAll——手動重新整理要讓使用者看到「真的在重新讀」，不是資料悄悄換掉。</summary>
    [RelayCommand]
    private async Task ApplyReloadAsync()
    {
        IsLoading = true;
        await ApplySnapshotCoreAsync(MinVisibleLoadingDuration);
    }

    [RelayCommand]
    private async Task ApplySetAutoStartAsync(InstalledToolSnapshot tool)
    {
        var (success, message) = await domain.UpdateToolSettingsAsync(new UpdateToolSettingsRequest
        {
            Id = tool.Id,
            AutoStart = !tool.AutoStart,
            AutoRepair = tool.AutoRepair,
        });
        notificationRuntime.ApplyShowResult(success, message);
    }

    [RelayCommand]
    private async Task ApplySetAutoRepairAsync(InstalledToolSnapshot tool)
    {
        var (success, message) = await domain.UpdateToolSettingsAsync(new UpdateToolSettingsRequest
        {
            Id = tool.Id,
            AutoStart = tool.AutoStart,
            AutoRepair = !tool.AutoRepair,
        });
        notificationRuntime.ApplyShowResult(success, message);
    }

    /// <summary>對齊 blueprint fieldClick：欄位在 LockedFields 內且尚未解鎖時，點擊不直接切換，
    /// 改成打開解鎖 Modal——跟 ApplySetAutoStartAsync 是兩個不同語意的入口，不是同一個操作的
    /// 參數分支（原則 2 的「前置條件本質不同」例外）。</summary>
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
        var (success, message) = await domain.UnlockFieldsAsync(new UnlockFieldsRequest
        {
            Id = tool.Id,
            ModuleKey = ModuleKeyInput,
        });
        if (success)
        {
            notificationRuntime.ApplyShowResult(true, message);
            ModuleKeyInput = "";
            UnlockError = null;
            UnlockPopupOpen = false;
        }
        else
        {
            // 對齊 blueprint confirmUnlock：金鑰錯誤時 Modal 留在原地、只顯示行內錯誤，
            // 不當作一般寫入失敗直接關窗——使用者應該能馬上重新輸入,不必再點一次「解鎖」。
            UnlockError = "金鑰錯誤，請重新輸入";
        }
    }

    #endregion
}
