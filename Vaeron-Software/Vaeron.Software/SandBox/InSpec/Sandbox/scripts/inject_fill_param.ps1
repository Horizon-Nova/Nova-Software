Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class Native {
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr addr, uint size, uint allocType, uint protect);
    [DllImport("kernel32.dll")] public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr addr, byte[] buffer, uint size, out UIntPtr written);
    [DllImport("kernel32.dll")] public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr sa, uint stackSize, IntPtr startAddr, IntPtr param, uint flags, out uint threadId);
    [DllImport("kernel32.dll")] public static extern uint WaitForSingleObject(IntPtr handle, uint ms);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] public static extern bool GetExitCodeThread(IntPtr h, out uint code);
    [DllImport("psapi.dll")] public static extern bool EnumProcessModulesEx(IntPtr hProcess, [Out] IntPtr[] modules, uint size, out uint needed, uint filterFlag);
    [DllImport("psapi.dll", CharSet=CharSet.Unicode)] public static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, System.Text.StringBuilder filename, uint size);
}
"@

$targetProcName = "InSpec"
$proc = Get-Process -Name $targetProcName -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Output "InSpec not running"; exit 1 }
$targetPid = $proc.Id
Write-Output "Target PID: $targetPid"

$PROCESS_ALL_ACCESS = 0x1F0FFF
$hProc = [Native]::OpenProcess($PROCESS_ALL_ACCESS, $false, $targetPid)
if ($hProc -eq [IntPtr]::Zero) { Write-Output "OpenProcess failed"; exit 1 }

$MEM_COMMIT = 0x1000; $PAGE_RW = 0x04

# Find the already-injected DLL's base address in the target process (no LoadLibrary here)
$modules = New-Object IntPtr[] 1024
$needed = 0
[Native]::EnumProcessModulesEx($hProc, $modules, [uint32]($modules.Length * [IntPtr]::Size), [ref]$needed, 3) | Out-Null
$count = [int]($needed / [IntPtr]::Size)
$targetBase = [IntPtr]::Zero
for ($i=0; $i -lt $count; $i++) {
    $sb = New-Object System.Text.StringBuilder 260
    [Native]::GetModuleFileNameEx($hProc, $modules[$i], $sb, 260) | Out-Null
    if ($sb.ToString() -like "*InSpecHookDLL.dll") { $targetBase = $modules[$i]; break }
}
if ($targetBase -eq [IntPtr]::Zero) { Write-Output "DLL not loaded in target process"; exit 1 }
Write-Output ("DLL base: 0x{0:X}" -f $targetBase.ToInt64())

# Write a UTF-16LE string into the target process memory
$testValue = "CDA343-AD-PARAM-TEST"
$strBytes = [System.Text.Encoding]::Unicode.GetBytes($testValue + "`0")
$strMem = [Native]::VirtualAllocEx($hProc, [IntPtr]::Zero, [uint32]$strBytes.Length, $MEM_COMMIT, $PAGE_RW)
$written = [UIntPtr]::Zero
[Native]::WriteProcessMemory($hProc, $strMem, $strBytes, [uint32]$strBytes.Length, [ref]$written) | Out-Null
Write-Output ("String written at: 0x{0:X}" -f $strMem.ToInt64())

# Call ThreadWrapper_AutoFillAndConfirmPrompt(lpParam = strMem address)
$rva = 0x4E60
$funcAddr = [IntPtr]($targetBase.ToInt64() + $rva)
$tid = 0
$hThread = [Native]::CreateRemoteThread($hProc, [IntPtr]::Zero, 0, $funcAddr, $strMem, 0, [ref]$tid)
if ($hThread -eq [IntPtr]::Zero) { Write-Output "CreateRemoteThread failed"; exit 1 }
[Native]::WaitForSingleObject($hThread, 5000) | Out-Null
$exitCode = 0
[Native]::GetExitCodeThread($hThread, [ref]$exitCode) | Out-Null
Write-Output "AutoFillAndConfirmPrompt(param) thread exit code: $exitCode"
[Native]::CloseHandle($hThread) | Out-Null
[Native]::CloseHandle($hProc) | Out-Null
