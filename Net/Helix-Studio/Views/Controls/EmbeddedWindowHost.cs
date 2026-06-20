using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Helix_Studio.Views.Controls;

/// <summary>
/// 把另一個程序的最上層視窗嵌入 WPF 內容區。
/// 作法：建立一個自有的容器子視窗交給 HwndHost（HwndHost 能可靠縮放自己建立的視窗），
/// 再把工具視窗 SetParent 進容器，並在容器 WM_SIZE 時把工具視窗填滿容器。
/// 不負責銷毀工具視窗（由工具程序擁有，停止時透過 Job Object 結束）。
/// </summary>
public sealed class EmbeddedWindowHost : HwndHost
{
    private const int GWL_STYLE = -16;
    private const long WS_CHILD = 0x40000000;
    private const long WS_VISIBLE = 0x10000000;
    private const long WS_CLIPCHILDREN = 0x02000000;
    private const long WS_CAPTION = 0x00C00000;
    private const long WS_THICKFRAME = 0x00040000;
    private const long WS_POPUP = unchecked((long)0x80000000);
    private const long WS_BORDER = 0x00800000;
    private const int WM_SIZE = 0x0005;
    private const int SW_SHOW = 5;

    private readonly nint _child;
    private nint _container;

    public EmbeddedWindowHost(nint childHandle) => _child = childHandle;

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        // 自有容器（內建 Static 類別），HwndHost 會負責縮放它
        _container = CreateWindowEx(0, "Static", string.Empty,
            (int)(WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN),
            0, 0, 0, 0, hwndParent.Handle, nint.Zero, nint.Zero, nint.Zero);

        // 去除工具視窗的標題/邊框，改為子視窗，放進容器
        long style = (long)GetWindowLongPtr(_child, GWL_STYLE);
        style &= ~(WS_CAPTION | WS_THICKFRAME | WS_POPUP | WS_BORDER);
        style |= WS_CHILD | WS_VISIBLE;
        SetWindowLongPtr(_child, GWL_STYLE, (nint)style);

        SetParent(_child, _container);
        ShowWindow(_child, SW_SHOW);

        return new HandleRef(this, _container);
    }

    protected override nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_SIZE)
            ResizeChildToContainer();
        return nint.Zero;
    }

    private void ResizeChildToContainer()
    {
        if (_container == nint.Zero || _child == nint.Zero) return;
        if (GetClientRect(_container, out RECT r))
            MoveWindow(_child, 0, 0, r.Right - r.Left, r.Bottom - r.Top, true);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        SetParent(_child, nint.Zero); // 工具視窗脫離，不銷毀
        if (_container != nint.Zero)
        {
            DestroyWindow(_container);
            _container = nint.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint CreateWindowEx(int exStyle, string className, string windowName,
        int style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetParent(nint hWndChild, nint hWndNewParent);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(nint hWnd, int x, int y, int width, int height, bool repaint);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);
}
