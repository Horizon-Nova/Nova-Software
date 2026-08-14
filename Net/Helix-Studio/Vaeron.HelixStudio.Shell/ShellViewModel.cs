using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vaeron.HelixStudio.Kernel;

namespace Vaeron.HelixStudio.Shell;

/// <summary>殼層版面狀態與命令：導覽歷史、三組導覽清單、三個可收合區塊、狀態列資料、AI 助理對話。</summary>
/// <remarks>兼任事件出口：各功能單元寫出的事件累加到使用中工作區的計數與未讀徽章。</remarks>
public sealed partial class ShellViewModel(IShellFacade facade) : ObservableObject, IEventSink
{
    private const int NavTransitionMilliseconds = 420;

    private static readonly WorkspaceInfo PlaceholderWorkspace =
        new("", "…", "", "…", "…", "…", 0, 0, 0, "…");

    /// <summary>每個導覽目的地的版面需求。新增畫面時加一列，殼層結構不動。</summary>
    private static readonly IReadOnlyList<ScreenLayout> Screens =
    [
        new ScreenLayout("assistant", false),
        new ScreenLayout("library", true),
        new ScreenLayout("events", true),
        new ScreenLayout("settings", true),
        new ScreenLayout("usb", true),
        new ScreenLayout("vnc", true)
    ];

    private readonly List<string> navHistory = ["assistant"];

    private IReadOnlyList<NavItem> mainNavSource = [];
    private IReadOnlyList<NavItem> toolsNavSource = [];
    private IReadOnlyList<NavItem> settingsNavSource = [];

    /// <summary>每次導覽取一個號碼，只有號碼最新的那次負責收掉載入狀態。</summary>
    private int navGeneration;

    public ObservableCollection<NavItemViewModel> MainNav { get; } = [];
    public ObservableCollection<NavItemViewModel> ToolsNav { get; } = [];
    public ObservableCollection<NavItemViewModel> SettingsNav { get; } = [];
    public ObservableCollection<ChatMessage> ChatMessages { get; } = [];

    public PanelToggle SidePanel { get; } = new("左側資訊欄", "Ctrl + B", true);
    public PanelToggle AiPanel { get; } = new("AI 助理面板", "Ctrl + Alt + B", true);
    public PanelToggle StatusBar { get; } = new("狀態列", "", true);

    public IReadOnlyList<PanelToggle> LayoutOptions => [SidePanel, AiPanel, StatusBar];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEventBadge))]
    private WorkspaceInfo activeWorkspace = PlaceholderWorkspace;

    [ObservableProperty]
    private string activeLabel = "";

    [ObservableProperty]
    private string activeNavId = "assistant";

    [ObservableProperty]
    private bool sidePanelVisible = true;

    [ObservableProperty]
    private bool customizeOpen;

    [ObservableProperty]
    private bool navContentLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEventBadge))]
    private bool showEventBadge = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(CanGoForward))]
    private int navIndex;

    public bool CanGoBack => NavIndex > 0;

    public bool CanGoForward => NavIndex < navHistory.Count - 1;

    public bool HasEventBadge => ShowEventBadge && ActiveWorkspace.EventBadge > 0;

    /// <summary>收下一筆事件：使用中工作區的事件數與未讀徽章各加一。</summary>
    public void Write(string level, string text)
        => ActiveWorkspace = ActiveWorkspace with
        {
            EventCount = ActiveWorkspace.EventCount + 1,
            EventBadge = ActiveWorkspace.EventBadge + 1
        };

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var snapshot = (await facade.LoadAsync(cancellationToken)).Value!;
        mainNavSource = snapshot.MainNav;
        toolsNavSource = snapshot.ToolsNav;
        settingsNavSource = snapshot.SettingsNav;
        ActiveWorkspace = snapshot.ActiveWorkspace;
        Fill(ChatMessages, snapshot.ChatMessages);

        SidePanel.PropertyChanged += (_, _) => UpdateSidePanelVisibility();
        ProjectNav();
    }

    [RelayCommand]
    private Task SetActiveNav(string id) => NavigateAsync(() => PushHistory(id));

    [RelayCommand]
    private Task GoToEvents() => SetActiveNav("events");

    [RelayCommand]
    private Task GoBack() => NavigateAsync(() => NavIndex = Math.Max(NavIndex - 1, 0));

    [RelayCommand]
    private Task GoForward() => NavigateAsync(() => NavIndex = Math.Min(NavIndex + 1, navHistory.Count - 1));

    [RelayCommand]
    private void OpenCustomizeLayout() => CustomizeOpen = !CustomizeOpen;

    [RelayCommand]
    private void CloseCustomize() => CustomizeOpen = false;

    private async Task NavigateAsync(Action applyNavState)
    {
        var generation = ++navGeneration;
        NavContentLoading = true;
        await Task.Delay(NavTransitionMilliseconds);
        applyNavState();
        ProjectNav();
        NavContentLoading = navGeneration != generation;
    }

    private void PushHistory(string id)
    {
        var kept = navHistory.Take(NavIndex + 1).Reverse().SkipWhile(visited => visited == id).Reverse();
        var next = kept.Append(id).ToList();
        navHistory.Clear();
        navHistory.AddRange(next);
        NavIndex = navHistory.Count - 1;
    }

    private void ProjectNav()
    {
        var activeId = navHistory.ElementAt(NavIndex);
        Fill(MainNav, Project(mainNavSource, activeId));
        Fill(ToolsNav, Project(toolsNavSource, activeId));
        Fill(SettingsNav, Project(settingsNavSource, activeId));

        ActiveLabel = MainNav.Concat(ToolsNav).Concat(SettingsNav)
            .Where(item => item.IsActive)
            .Select(item => item.Label)
            .FirstOrDefault("");

        ActiveNavId = activeId;
        UpdateSidePanelVisibility();
    }

    private void UpdateSidePanelVisibility()
        => SidePanelVisible = SidePanel.IsOpen && WantsSidePanel(ActiveNavId);

    private static bool WantsSidePanel(string navId)
        => Screens.Where(screen => screen.NavId == navId).Select(screen => screen.ShowsSidePanel).Single();

    private static IEnumerable<NavItemViewModel> Project(IReadOnlyList<NavItem> source, string activeId)
        => source.Select(item => new NavItemViewModel(item.Id, item.Icon, item.Label, item.HasIcon, item.Id == activeId));

    private static void Fill<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}
