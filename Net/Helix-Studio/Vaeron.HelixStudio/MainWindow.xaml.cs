using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;

namespace Vaeron.HelixStudio;

public partial class MainWindow : Window
{
    private readonly Themes.ThemePackService _themePacks;
    private Storyboard? _backgroundLoop;

    public MainWindow(MainShellViewModel viewModel, Themes.ThemePackService themePacks)
    {
        InitializeComponent();
        DataContext = viewModel;
        _themePacks = themePacks;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _themePacks.ThemeApplied += ApplyBackgroundMedia;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainShellViewModel.BackgroundAnimationEnabled))
        {
            ApplyBackgroundMedia();
            return;
        }
        if (e.PropertyName != nameof(MainShellViewModel.UiScale)) return;

        ResizeToScaledContent();
    }

    /// <summary>縮放後依 ScaleRoot 的實際需求尺寸撐大視窗（只撐大，不縮小）</summary>
    private void ResizeToScaledContent()
    {
        ScaleRoot.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = ScaleRoot.DesiredSize;

        const double borderAllowance = 2; // 外層 Border BorderThickness="1" 兩側
        if (Width < desired.Width + borderAllowance) Width = desired.Width + borderAllowance;
        if (Height < desired.Height + borderAllowance) Height = desired.Height + borderAllowance;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ((MainShellViewModel)DataContext).ApplyShellLoaded();
        ApplyBackgroundMedia();
    }

    /// <summary>套用主題宣告的背景媒體。素材與呈現方式歸主題，播放與循環歸軟體。</summary>
    private void ApplyBackgroundMedia()
    {
        _backgroundLoop?.Stop(this);
        _backgroundLoop = null;
        BackgroundMedia.Visibility = Visibility.Collapsed;

        if (!((MainShellViewModel)DataContext).BackgroundAnimationEnabled) return;
        if (TryFindResource("BackgroundMedia") is not string path || string.IsNullOrWhiteSpace(path)) return;
        if (!System.IO.File.Exists(path)) return;

        var (withinLimit, duration) = Themes.BackgroundMediaLimits.Evaluate(path);
        if (!withinLimit) return;

        var timeline = new MediaTimeline(new Uri(path, UriKind.RelativeOrAbsolute))
        {
            RepeatBehavior = RepeatBehavior.Forever,
        };
        // 播放器對 GIF 不回報結束也不保證回報時長，改由檔案自己的幀延遲決定循環區間
        if (duration > TimeSpan.Zero) timeline.Duration = new Duration(duration);
        Storyboard.SetTarget(timeline, BackgroundMedia);

        _backgroundLoop = new Storyboard();
        _backgroundLoop.Children.Add(timeline);

        BackgroundMedia.Visibility = Visibility.Visible;
        _backgroundLoop.Begin(this, true);
    }

    /// <summary>視窗未在使用時暫停背景動畫</summary>
    private void OnWindowActivated(object? sender, EventArgs e) => _backgroundLoop?.Resume(this);

    private void OnWindowDeactivated(object? sender, EventArgs e) => _backgroundLoop?.Pause(this);

    private void OnClosed(object? sender, EventArgs e)
    {
        var vm = (MainShellViewModel)DataContext;
        vm.PropertyChanged -= OnViewModelPropertyChanged;
        _themePacks.ThemeApplied -= ApplyBackgroundMedia;
        _backgroundLoop?.Stop(this);
        vm.ApplyShellUnloaded();
    }

    /// <summary>側邊欄拖曳調整寬度</summary>
    private void OnSidebarResizeDragDelta(object sender, DragDeltaEventArgs e)
        => ((MainShellViewModel)DataContext).ApplyResizeSidebar(e.HorizontalChange);

    /// <summary>自繪標題列拖曳／雙擊最大化</summary>
    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
            return;
        }

        if (WindowState == WindowState.Maximized)
        {
            var pointInWindow = e.GetPosition(this);
            var screenPoint = PointToScreen(pointInWindow);
            var relativeX = pointInWindow.X / ActualWidth;

            WindowState = WindowState.Normal;

            Left = screenPoint.X - (RestoreBounds.Width * relativeX);
            Top = screenPoint.Y - pointInWindow.Y;
        }

        DragMove();
    }

    /// <summary>掛視窗訊息鉤子、套用圓角</summary>
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
        ApplyRoundedCorners(handle);
    }

    /// <summary>套用 Windows 11 原生圓角</summary>
    private static void ApplyRoundedCorners(IntPtr hwnd)
    {
        const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        const int DWMWCP_ROUND = 2;
        var preference = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;
        if (msg == WM_GETMINMAXINFO)
        {
            ApplyWorkAreaToMinMaxInfo(hwnd, lParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>依工作區覆寫最大化座標與最小尺寸</summary>
    private void ApplyWorkAreaToMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

        const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            GetMonitorInfo(monitor, ref monitorInfo);
            var workArea = monitorInfo.rcWork;
            var monitorArea = monitorInfo.rcMonitor;

            mmi.ptMaxPosition.X = Math.Abs(workArea.Left - monitorArea.Left);
            mmi.ptMaxPosition.Y = Math.Abs(workArea.Top - monitorArea.Top);
            mmi.ptMaxSize.X = Math.Abs(workArea.Right - workArea.Left);
            mmi.ptMaxSize.Y = Math.Abs(workArea.Bottom - workArea.Top);
        }

        var dpiScale = VisualTreeHelper.GetDpi(this);
        mmi.ptMinTrackSize.X = (int)Math.Round(MinWidth * dpiScale.DpiScaleX);
        mmi.ptMinTrackSize.Y = (int)Math.Round(MinHeight * dpiScale.DpiScaleY);

        Marshal.StructureToPtr(mmi, lParam, true);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
        => SystemCommands.MinimizeWindow(this);

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
        => ToggleMaximizeRestore();

    private void OnCloseClick(object sender, RoutedEventArgs e)
        => SystemCommands.CloseWindow(this);

    private void ToggleMaximizeRestore()
    {
        if (WindowState == WindowState.Maximized)
            SystemCommands.RestoreWindow(this);
        else
            SystemCommands.MaximizeWindow(this);
    }
}
