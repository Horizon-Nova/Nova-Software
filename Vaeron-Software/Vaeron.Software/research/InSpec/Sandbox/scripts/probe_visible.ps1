Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class W {
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
}
"@
$h = [W]::FindWindow("InSpecAppFrame", $null)
if ($h -eq [IntPtr]::Zero) { Write-Output "InSpecAppFrame: NOT FOUND (no InSpec window at all)" }
else { Write-Output ("InSpecAppFrame hwnd=0x{0:X}  IsWindowVisible={1}" -f $h.ToInt64(), [W]::IsWindowVisible($h)) }
