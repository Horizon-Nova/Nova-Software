using System;
using System.Runtime.InteropServices;

namespace Helix_Studio.Infrastructure.Utilities;

/// <summary>查詢使用者最後輸入至今之閒置時間（Windows 使用者層）。</summary>
public static class IdleTimeUtility
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    /// <summary>回傳自上次鍵鼠輸入起算之閒置時間；失敗時為零。</summary>
    public static TimeSpan QueryIdleTime()
    {
        var lastInput = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref lastInput))
            return TimeSpan.Zero;

        var tick = unchecked((uint)Environment.TickCount);
        var idleMs = tick - lastInput.dwTime;
        return TimeSpan.FromMilliseconds(idleMs);
    }
}
