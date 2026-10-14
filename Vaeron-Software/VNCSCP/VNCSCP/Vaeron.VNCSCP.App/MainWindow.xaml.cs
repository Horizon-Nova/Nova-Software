using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Vaeron.VNCSCP.Dashboard;

namespace Vaeron.VNCSCP.App;

public partial class MainWindow : Window
{
    private const int WindowCornerPreference = 33;
    private const int RoundedCorner = 2;

    /// <summary>視窗狀態決定標題列按鈕按下去要做什麼。</summary>
    private static readonly Dictionary<WindowState, Action<Window>> ToggleByState = new()
    {
        [WindowState.Normal] = SystemCommands.MaximizeWindow,
        [WindowState.Minimized] = SystemCommands.MaximizeWindow,
        [WindowState.Maximized] = SystemCommands.RestoreWindow
    };

    private static readonly Dictionary<WindowState, Visibility> MaximizeIconByState = new()
    {
        [WindowState.Normal] = Visibility.Visible,
        [WindowState.Minimized] = Visibility.Visible,
        [WindowState.Maximized] = Visibility.Collapsed
    };

    private static readonly Dictionary<WindowState, Visibility> RestoreIconByState = new()
    {
        [WindowState.Normal] = Visibility.Collapsed,
        [WindowState.Minimized] = Visibility.Collapsed,
        [WindowState.Maximized] = Visibility.Visible
    };

    private static readonly Dictionary<WindowState, string> ToolTipByState = new()
    {
        [WindowState.Normal] = "最大化",
        [WindowState.Minimized] = "最大化",
        [WindowState.Maximized] = "還原"
    };

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    public MainWindow(ShellCoordinator coordinator, DashboardView dashboardView)
    {
        InitializeComponent();
        coordinator.Attach(this, StatusText);
        ContentHost.Content = dashboardView;

        StateChanged += OnStateChanged;
        Closed += OnClosedCleanup;
        ApplyStateIcons();
    }

    /// <summary>視窗控柄建立後套用圓角。</summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var preference = RoundedCorner;
        _ = DwmSetWindowAttribute(hwnd, WindowCornerPreference, ref preference, sizeof(int));
    }

    private void OnStateChanged(object? sender, EventArgs e) => ApplyStateIcons();

    private void OnClosedCleanup(object? sender, EventArgs e)
    {
        StateChanged -= OnStateChanged;
        Closed -= OnClosedCleanup;
    }

    private void ApplyStateIcons()
    {
        MaximizeIcon.Visibility = MaximizeIconByState.GetValueOrDefault(WindowState, Visibility.Visible);
        RestoreIcon.Visibility = RestoreIconByState.GetValueOrDefault(WindowState, Visibility.Collapsed);
        MaximizeRestoreButton.ToolTip = ToolTipByState.GetValueOrDefault(WindowState, "最大化");
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
        => ToggleByState.GetValueOrDefault(WindowState, SystemCommands.MaximizeWindow)(this);

    private void OnCloseClick(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);
}

