using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace Vaeron.HelixStudio;

public partial class MainWindow : Window
{
    public MainWindow(MainShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
        => ((MainShellViewModel)DataContext).ApplyShellLoaded();

    private void OnClosed(object? sender, EventArgs e)
        => ((MainShellViewModel)DataContext).ApplyShellUnloaded();

    /// <summary>Thumb.DragDelta 沒有內建的 Command 綁定機制（不像 Button.Command），
    /// 這裡是唯一合理的接線點——跟 OnTitleBarMouseLeftButtonDown 同一種「WPF 事件沒有
    /// Command 對應、code-behind 轉呼叫 ViewModel 方法」模式，不是繞過 MVVM。</summary>
    private void OnSidebarResizeDragDelta(object sender, DragDeltaEventArgs e)
        => ((MainShellViewModel)DataContext).ApplyResizeSidebar(e.HorizontalChange);

    /// <summary>自繪標題列的拖曳/雙擊最大化——WindowChrome.CaptionHeight 刻意設 0（見 XAML
    /// 註解），改成頂部列自己接手：按鈕本身會在 ButtonBase 內部處理掉 MouseLeftButtonDown
    /// （標記 Handled），所以點在按鈕上不會冒泡到這裡；只有點在頂部列空白處才會觸發拖曳。
    /// 最大化狀態下直接呼叫 DragMove() 不會有 Windows 使用者熟悉的「從最大化拖曳會先還原、
    /// 視窗跟著游標」效果——CaptionHeight=0 代表這個行為不是框架免費附送的，這裡手動補：
    /// 還原視窗，並把視窗定位成游標落在還原後同一個相對水平位置，讓還原銜接 DragMove()
    /// 時視覺上像是直接把視窗從最大化「撕」下來。</summary>
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

    /// <summary>WindowStyle=None 的無邊框視窗，最大化時 WPF 預設會把視窗撐滿「整個螢幕」
    /// （含工作列區域），不是「工作區」——有邊框的一般視窗這個調整是 Windows 自動處理，
    /// 無邊框視窗必須自己接手，否則底部狀態列會被工作列蓋住或擠出畫面外。掛
    /// WM_GETMINMAXINFO 訊息鉤子，依目前所在螢幕的工作區（rcWork，已扣掉工作列）
    /// 覆寫最大化時的座標與尺寸，多螢幕情境用 MonitorFromWindow 抓最近的螢幕。</summary>
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
        ApplyRoundedCorners(handle);
    }

    /// <summary>WindowChrome 自繪標題列（CaptionHeight=0）的視窗，Windows 11 不會自動套用
    /// 原生圓角——那個自動效果只給沒有自訂 chrome 的標準視窗。DWMWA_WINDOW_CORNER_PREFERENCE
    /// 是 Windows 11 才有的屬性（Windows 10 呼叫會失敗，直接忽略回傳值、維持直角，不視為錯誤）。
    /// 最大化時 DWM 本來就會自動改回直角（滿版視窗圓角沒有意義），不需要另外處理。</summary>
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

    /// <summary>標記 handled=true 代表 WPF 內建「依 MinWidth/MinHeight 設定 ptMinTrackSize」
    /// 的處理不會再跑，這裡要自己補上，否則 MinWidth/MinHeight 形同虛設、視窗可以被拖到
    /// 任意小，頂部列的圖示群組會被視窗邊界硬生生切斷（縮到很小時右側視窗控制鈕消失／
    /// 被裁掉的成因）——這是上一輪修「最大化蓋掉工作列」時漏掉的一半，不是新問題。</summary>
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
