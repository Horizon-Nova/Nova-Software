using System.Runtime.InteropServices;
using System.Text;

namespace ExternalModules.InSpecTool.Utilities;

public static class InSpecRunButtonUtility
{
    private const uint WmCommand = 0x0111;
    private const uint MfByPosition = 0x0400;
    private const uint MfByCommand = 0x0000;
    private const uint MfGrayed = 0x0001;
    private const uint MfDisabled = 0x0002;

    private static readonly string[] PlaybackMenuCandidates =
    [
        "playback",
        "auto measure",
        "自動量測"
    ];

    private static readonly string[] RunMenuCandidates =
    [
        "run",
        "執行"
    ];

    public static void StartAutoRunAfterOpen(bool enableAutoRun)
    {
        if (!enableAutoRun)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(600).ConfigureAwait(false);
                TryClickRunButton();
            }
            catch
            {
            }
        });
    }

    public static bool TryClickRunButton()
    {
        var inspecWindow = FindInSpecMainWindow();
        if (inspecWindow == nint.Zero)
        {
            return false;
        }

        NativeMethods.SetForegroundWindow(inspecWindow);
        Thread.Sleep(200);

        var menuHandle = NativeMethods.GetMenu(inspecWindow);
        if (menuHandle == nint.Zero)
        {
            return false;
        }

        var runCommandId = DetectRunCommandId(menuHandle);
        if (runCommandId <= 0)
        {
            return false;
        }

        var startAt = Environment.TickCount64;
        while (Environment.TickCount64 - startAt < 5000)
        {
            var state = NativeMethods.GetMenuState(menuHandle, (uint)runCommandId, MfByCommand);
            if (state != uint.MaxValue)
            {
                var isDisabled = (state & (MfGrayed | MfDisabled)) != 0;
                if (!isDisabled)
                {
                    break;
                }
            }

            Thread.Sleep(150);
        }

        NativeMethods.SendMessage(inspecWindow, WmCommand, new nint(runCommandId), nint.Zero);
        return true;
    }

    private static int DetectRunCommandId(nint menuHandle)
    {
        var menuCount = NativeMethods.GetMenuItemCount(menuHandle);
        if (menuCount <= 0)
        {
            return 0;
        }

        for (var menuIndex = 0; menuIndex < menuCount; menuIndex++)
        {
            var menuText = NormalizeMenuText(QueryMenuText(menuHandle, (uint)menuIndex));
            if (!QueryMatchesAny(menuText, PlaybackMenuCandidates))
            {
                continue;
            }

            var subMenuHandle = NativeMethods.GetSubMenu(menuHandle, menuIndex);
            if (subMenuHandle == nint.Zero)
            {
                continue;
            }

            var subMenuCount = NativeMethods.GetMenuItemCount(subMenuHandle);
            if (subMenuCount <= 0)
            {
                continue;
            }

            for (var itemIndex = 0; itemIndex < subMenuCount; itemIndex++)
            {
                var itemText = NormalizeMenuText(QueryMenuText(subMenuHandle, (uint)itemIndex));
                if (!QueryMatchesAny(itemText, RunMenuCandidates))
                {
                    continue;
                }

                var commandId = NativeMethods.GetMenuItemID(subMenuHandle, itemIndex);
                if (commandId > 0)
                {
                    return commandId;
                }
            }
        }

        return 0;
    }

    private static nint FindInSpecMainWindow()
    {
        nint foundWindow = nint.Zero;
        NativeMethods.EnumWindows((windowHandle, _) =>
        {
            if (!NativeMethods.IsWindowVisible(windowHandle))
            {
                return true;
            }

            var className = NativeMethods.QueryClassName(windowHandle);
            var title = NativeMethods.QueryWindowText(windowHandle).Trim();
            if (string.Equals(className, "InSpecAppFrame", StringComparison.OrdinalIgnoreCase) ||
                title.StartsWith("InSpec for Windows", StringComparison.OrdinalIgnoreCase))
            {
                foundWindow = windowHandle;
                return false;
            }

            return true;
        }, nint.Zero);

        return foundWindow;
    }

    private static string QueryMenuText(nint menuHandle, uint itemIndex)
    {
        var buffer = new StringBuilder(512);
        var copied = NativeMethods.GetMenuString(menuHandle, itemIndex, buffer, buffer.Capacity, MfByPosition);
        return copied <= 0 ? string.Empty : buffer.ToString();
    }

    private static string NormalizeMenuText(string text) =>
        text
            .Replace("&", string.Empty, StringComparison.Ordinal)
            .Split('\t')[0]
            .Trim()
            .ToLowerInvariant();

    private static bool QueryMatchesAny(string normalizedText, IEnumerable<string> candidates) =>
        candidates.Any(candidate => normalizedText.Contains(candidate, StringComparison.OrdinalIgnoreCase));

    private static class NativeMethods
    {
        internal delegate bool EnumWindowsProc(nint windowHandle, nint lParam);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

        [DllImport("user32.dll")]
        internal static extern bool IsWindowVisible(nint windowHandle);

        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(nint windowHandle);

        [DllImport("user32.dll")]
        internal static extern nint GetMenu(nint windowHandle);

        [DllImport("user32.dll")]
        internal static extern int GetMenuItemCount(nint menuHandle);

        [DllImport("user32.dll")]
        internal static extern nint GetSubMenu(nint menuHandle, int position);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetMenuString(
            nint menuHandle,
            uint itemId,
            StringBuilder buffer,
            int maxCount,
            uint flags);

        [DllImport("user32.dll")]
        internal static extern int GetMenuItemID(nint menuHandle, int position);

        [DllImport("user32.dll")]
        internal static extern uint GetMenuState(nint menuHandle, uint itemId, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern nint SendMessage(nint windowHandle, uint message, nint wParam, nint lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowTextW(nint windowHandle, StringBuilder buffer, int maxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowTextLengthW(nint windowHandle);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetClassNameW(nint windowHandle, StringBuilder buffer, int maxCount);

        internal static string QueryWindowText(nint windowHandle)
        {
            var textLength = GetWindowTextLengthW(windowHandle);
            if (textLength <= 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(textLength + 1);
            _ = GetWindowTextW(windowHandle, builder, builder.Capacity);
            return builder.ToString();
        }

        internal static string QueryClassName(nint windowHandle)
        {
            var builder = new StringBuilder(256);
            _ = GetClassNameW(windowHandle, builder, builder.Capacity);
            return builder.ToString();
        }
    }
}
