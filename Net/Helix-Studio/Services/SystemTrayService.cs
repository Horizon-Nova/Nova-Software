using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using Helix_Studio.Services.Tools;

namespace Helix_Studio.Services;

public sealed class SystemTrayService : IDisposable
{
    private const int WM_TRAYNOTIFY = 0x8001;
    private const int NIM_ADD = 0;
    private const int NIM_MODIFY = 1;
    private const int NIM_DELETE = 2;
    private const int NIF_MESSAGE = 0x1;
    private const int NIF_ICON = 0x2;
    private const int NIF_TIP = 0x4;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONUP = 0x0205;
    private const int IDI_APPLICATION = 32512;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public nint hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern nint LoadIcon(nint hInstance, nint lpIconName);

    private HwndSource? _hwndSource;
    private NOTIFYICONDATA _nid;
    private bool _iconAdded;
    private Window? _window;
    private bool _suppressClose;
    private ContextMenu? _contextMenu;
    private readonly AppSettingsService _appSettings;

    public event Action? RestoreRequested;
    public event Action? ExitRequested;
    public event Action<string>? QuickLaunchRequested;
    public event Action<string>? QuickStopRequested;

    public SystemTrayService(ToolProcessService processService, AppSettingsService appSettings)
    {
        _appSettings = appSettings;
        var hp = new HwndSourceParameters("HelixTrayMsg")
        {
            WindowStyle = 0,
            Width = 0,
            Height = 0,
            PositionX = 0,
            PositionY = 0,
            ParentWindow = (nint)(-3),
        };
        _hwndSource = new HwndSource(hp);
        _hwndSource.AddHook(WndProc);

        _nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwndSource.Handle,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYNOTIFY,
            hIcon = LoadIcon(nint.Zero, new nint(IDI_APPLICATION)),
            szTip = "Helix Studio",
        };
    }

    public void Attach(Window window)
    {
        _window = window;
        _window.Closing += OnWindowClosing;
    }

    public void ShowIcon()
    {
        if (!_iconAdded)
        {
            Shell_NotifyIcon(NIM_ADD, ref _nid);
            _iconAdded = true;
        }
        else
        {
            Shell_NotifyIcon(NIM_MODIFY, ref _nid);
        }
    }

    public void HideIcon()
    {
        if (!_iconAdded) return;
        Shell_NotifyIcon(NIM_DELETE, ref _nid);
        _iconAdded = false;
    }

    public void UpdateTooltip(int runningCount)
    {
        _nid.szTip = runningCount > 0
            ? LocalizationService.Instance.Format("L.Tray.TooltipRunning", runningCount)
            : "Helix Studio";
        if (_iconAdded) Shell_NotifyIcon(NIM_MODIFY, ref _nid);
    }

    public void RefreshContextMenu(IEnumerable<(string Id, string Name, bool IsRunning)> tools)
    {
        var menu = new ContextMenu();

        var showItem = new MenuItem { Header = LocalizationService.Instance.GetString("L.Tray.Show") };
        showItem.Click += (_, _) => RestoreRequested?.Invoke();
        menu.Items.Add(showItem);
        menu.Items.Add(new Separator());

        foreach (var (id, name, isRunning) in tools)
        {
            var capturedId = id;
            var capturedRunning = isRunning;
            var item = new MenuItem { Header = isRunning ? $"⬤ {name}" : $"○ {name}" };
            item.Click += (_, _) =>
            {
                if (capturedRunning) QuickStopRequested?.Invoke(capturedId);
                else QuickLaunchRequested?.Invoke(capturedId);
            };
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        var exitItem = new MenuItem { Header = LocalizationService.Instance.GetString("L.Tray.Exit") };
        exitItem.Click += (_, _) => ExitRequested?.Invoke();
        menu.Items.Add(exitItem);

        _contextMenu = menu;
    }

    public void MinimizeToTray()
    {
        if (_window is null) return;
        _suppressClose = true;
        _window.Hide();
        _suppressClose = false;
    }

    public void RestoreFromTray()
    {
        if (_window is null) return;
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != WM_TRAYNOTIFY) return nint.Zero;

        var mouseMsg = (int)(lParam.ToInt64() & 0xFFFF);
        switch (mouseMsg)
        {
            case WM_LBUTTONDOWN:
                handled = true;
                Application.Current?.Dispatcher.Invoke(() => RestoreRequested?.Invoke());
                break;
            case WM_RBUTTONUP:
                handled = true;
                Application.Current?.Dispatcher.Invoke(ShowContextMenu);
                break;
        }
        return nint.Zero;
    }

    private void ShowContextMenu()
    {
        if (_contextMenu is null) return;
        _contextMenu.Placement = PlacementMode.MousePoint;
        _contextMenu.IsOpen = true;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_suppressClose) return;
        if (!_appSettings.Settings.CloseToTray) return;
        e.Cancel = true;
        MinimizeToTray();
    }

    public void Dispose()
    {
        HideIcon();
        _hwndSource?.Dispose();
        _hwndSource = null;
    }
}
