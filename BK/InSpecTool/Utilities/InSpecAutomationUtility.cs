using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using ExternalModules.InSpecTool.Core;

namespace ExternalModules.InSpecTool.Utilities;

public sealed class InSpecAutomationUtility
{
    private const uint WmSetText = 0x000C;
    private const uint BmClick = 0x00F5;
    private const uint GwOwner = 4;
    private const int DefaultDebounceMs = 600;
    private static readonly string[] ConfirmButtonTexts = ["OK", "確定"];

    private readonly object _syncRoot = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastTriggeredAt = new(StringComparer.Ordinal);
    private InSpecToolConfiguration _configuration = new();
    private nint _lastTriggeredDialogHandle;

    public void UpdateConfiguration(InSpecToolConfiguration configuration)
    {
        lock (_syncRoot)
        {
            _configuration = configuration;
        }
    }

    public InSpecToolConfiguration QueryConfigurationSnapshot()
    {
        lock (_syncRoot)
        {
            return _configuration;
        }
    }

    public int QueryMonitorIntervalMs()
    {
        var configuration = QueryConfigurationSnapshot();
        return Math.Clamp(configuration.MonitorIntervalMs, 100, 10000);
    }

    public string? QueryAutoInputRequest()
    {
        var configuration = QueryConfigurationSnapshot();
        if (!configuration.EnableAutoMonitoring)
        {
            return null;
        }

        var match = QueryPromptMatch(configuration.FieldDefinitions);
        if (match is null || ShouldDebounce(match.Value.InputTypeName, match.Value.DialogHandle))
        {
            return null;
        }

        return match.Value.InputTypeName;
    }

    public bool TryAutoInput(string value)
    {
        var dialogHandle = NativeMethods.GetForegroundWindow();
        if (dialogHandle == nint.Zero)
        {
            return false;
        }

        var inputHandle = FindFirstChildByClassName(dialogHandle, "Edit");
        if (inputHandle == nint.Zero)
        {
            return false;
        }

        NativeMethods.SetForegroundWindow(dialogHandle);
        Thread.Sleep(100);

        if (!NativeMethods.SendMessage(inputHandle, WmSetText, nint.Zero, value ?? string.Empty))
        {
            return false;
        }

        Thread.Sleep(100);
        var confirmButton = FindConfirmButton(dialogHandle);
        if (confirmButton == nint.Zero)
        {
            return false;
        }

        return NativeMethods.SendMessage(confirmButton, BmClick, nint.Zero, nint.Zero);
    }

    private (string InputTypeName, nint DialogHandle)? QueryPromptMatch(IReadOnlyList<InSpecFieldDefinition> definitions)
    {
        var dialogHandle = NativeMethods.GetForegroundWindow();
        if (dialogHandle == nint.Zero || !BelongsToInSpec(dialogHandle))
        {
            return null;
        }

        var promptText = QueryPromptText(dialogHandle);
        if (string.IsNullOrWhiteSpace(promptText))
        {
            return null;
        }

        foreach (var definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.Keyword))
            {
                continue;
            }

            if (promptText.Contains(definition.Keyword.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return (definition.Name, dialogHandle);
            }
        }

        return null;
    }

    private bool ShouldDebounce(string inputTypeName, nint dialogHandle)
    {
        if (dialogHandle != nint.Zero && dialogHandle == _lastTriggeredDialogHandle)
        {
            return true;
        }

        var now = DateTimeOffset.UtcNow;
        if (_lastTriggeredAt.TryGetValue(inputTypeName, out var previousTriggeredAt) &&
            (now - previousTriggeredAt).TotalMilliseconds < DefaultDebounceMs)
        {
            return true;
        }

        _lastTriggeredAt[inputTypeName] = now;
        _lastTriggeredDialogHandle = dialogHandle;
        return false;
    }

    private static bool BelongsToInSpec(nint dialogHandle)
    {
        var mainWindow = FindInSpecMainWindow();
        if (mainWindow == nint.Zero)
        {
            return false;
        }

        var current = dialogHandle;
        while (current != nint.Zero)
        {
            if (current == mainWindow)
            {
                return true;
            }

            current = NativeMethods.GetWindow(current, GwOwner);
        }

        return false;
    }

    private static nint FindInSpecMainWindow()
    {
        var matches = new List<nint>();
        NativeMethods.EnumWindows((windowHandle, _) =>
        {
            if (!NativeMethods.IsWindowVisible(windowHandle))
            {
                return true;
            }

            var className = NativeMethods.QueryClassName(windowHandle);
            var title = NativeMethods.QueryWindowText(windowHandle);
            if (string.Equals(className, "InSpecAppFrame", StringComparison.OrdinalIgnoreCase) ||
                title.StartsWith("InSpec for Windows", StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(windowHandle);
            }

            return true;
        }, nint.Zero);

        return matches.FirstOrDefault();
    }

    private static string QueryPromptText(nint dialogHandle)
    {
        var bestText = string.Empty;
        NativeMethods.EnumChildWindows(dialogHandle, (childHandle, _) =>
        {
            if (!string.Equals(NativeMethods.QueryClassName(childHandle), "Static", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var text = NativeMethods.QueryWindowText(childHandle).Trim();
            if (text.Length > bestText.Length)
            {
                bestText = text;
            }

            return true;
        }, nint.Zero);

        return bestText;
    }

    private static nint FindFirstChildByClassName(nint parentHandle, string className)
    {
        var foundHandle = nint.Zero;
        NativeMethods.EnumChildWindows(parentHandle, (childHandle, _) =>
        {
            if (string.Equals(NativeMethods.QueryClassName(childHandle), className, StringComparison.OrdinalIgnoreCase))
            {
                foundHandle = childHandle;
                return false;
            }

            return true;
        }, nint.Zero);

        return foundHandle;
    }

    private static nint FindConfirmButton(nint dialogHandle)
    {
        var foundHandle = nint.Zero;
        NativeMethods.EnumChildWindows(dialogHandle, (childHandle, _) =>
        {
            if (!string.Equals(NativeMethods.QueryClassName(childHandle), "Button", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var buttonText = NativeMethods.QueryWindowText(childHandle).Trim();
            if (ConfirmButtonTexts.Any(text => string.Equals(text, buttonText, StringComparison.OrdinalIgnoreCase)))
            {
                foundHandle = childHandle;
                return false;
            }

            return true;
        }, nint.Zero);

        return foundHandle;
    }

    private static class NativeMethods
    {
        internal delegate bool EnumWindowsProc(nint windowHandle, nint lParam);

        [DllImport("user32.dll")]
        internal static extern nint GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool EnumChildWindows(nint parentHandle, EnumWindowsProc callback, nint lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowTextW(nint windowHandle, StringBuilder buffer, int maxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowTextLengthW(nint windowHandle);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetClassNameW(nint windowHandle, StringBuilder buffer, int maxCount);

        [DllImport("user32.dll")]
        internal static extern bool IsWindowVisible(nint windowHandle);

        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(nint windowHandle);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern nint SendMessageW(nint windowHandle, uint message, nint wParam, string lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern nint SendMessageW(nint windowHandle, uint message, nint wParam, nint lParam);

        [DllImport("user32.dll")]
        internal static extern nint GetWindow(nint windowHandle, uint command);

        internal static bool SendMessage(nint windowHandle, uint message, nint wParam, string value) =>
            SendMessageW(windowHandle, message, wParam, value) != nint.Zero || message == WmSetText;

        internal static bool SendMessage(nint windowHandle, uint message, nint wParam, nint lParam) =>
            SendMessageW(windowHandle, message, wParam, lParam) != nint.Zero || message == BmClick;

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
