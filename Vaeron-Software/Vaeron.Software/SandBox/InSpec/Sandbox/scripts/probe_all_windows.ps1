Add-Type -TypeDefinition @"
using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;
public class W2 {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    public static List<string> ListForPid(int targetPid) {
        var results = new List<string>();
        EnumWindows((hWnd, lParam) => {
            int pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (pid == targetPid) {
                var cls = new StringBuilder(256);
                GetClassName(hWnd, cls, 256);
                var txt = new StringBuilder(256);
                GetWindowText(hWnd, txt, 256);
                bool vis = IsWindowVisible(hWnd);
                results.Add(string.Format("hwnd=0x{0:X} class={1} visible={2} text=\"{3}\"", hWnd.ToInt64(), cls, vis, txt));
            }
            return true;
        }, IntPtr.Zero);
        return results;
    }
}
"@

$targetPid = [int]$args[0]
$list = [W2]::ListForPid($targetPid)
if ($list.Count -eq 0) { Write-Output "No top-level windows found for PID $targetPid" }
else { $list | ForEach-Object { Write-Output $_ } }
