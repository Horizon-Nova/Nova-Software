using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Helix_Studio.Interop;

/// <summary>
/// 修正 WindowStyle=None 視窗最大化時超出螢幕工作區的問題。
/// 根因：borderless 視窗最大化時 Windows 預設把視窗放大到比工作區更大，
/// 導致底部內容被推出螢幕下緣。
/// 解法：攔截 WM_GETMINMAXINFO，將最大化的位置與尺寸限制在目前螢幕的工作區
/// （rcWork，已排除工作列），不依賴任何 Margin 猜測值，自動對應各 DPI 與多螢幕。
/// </summary>
public static class MaximizeToWorkArea
{
    private const int WM_GETMINMAXINFO = 0x0024;
    private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

    public static void Apply(Window window)
    {
        // HWND 要等 SourceInitialized 後才存在；建構式階段 handle 為 0，需延後掛勾。
        if (new WindowInteropHelper(window).Handle != nint.Zero)
            Hook(window);
        else
            window.SourceInitialized += (_, _) => Hook(window);
    }

    private static void Hook(Window window)
    {
        nint handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero) return;
        HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
    }

    private static nint WindowProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            ConstrainToWorkArea(hwnd, lParam);
            handled = true;
        }
        return nint.Zero;
    }

    private static void ConstrainToWorkArea(nint hwnd, nint lParam)
    {
        nint monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor == nint.Zero) return;

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info)) return;

        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        RECT work = info.rcWork;
        RECT screen = info.rcMonitor;

        // 位置與尺寸都以「相對於螢幕原點」表示，扣掉工作列佔用的區域。
        mmi.ptMaxPosition.X = work.Left - screen.Left;
        mmi.ptMaxPosition.Y = work.Top - screen.Top;
        mmi.ptMaxSize.X = work.Right - work.Left;
        mmi.ptMaxSize.Y = work.Bottom - work.Top;

        Marshal.StructureToPtr(mmi, lParam, true);
    }

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, int flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO info);

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
}
