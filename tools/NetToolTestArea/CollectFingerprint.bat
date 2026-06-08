@echo off
setlocal

set "SCRIPT_PATH=%TEMP%\CollectFingerprint_%RANDOM%%RANDOM%.ps1"

(
echo $ErrorActionPreference = 'SilentlyContinue'
echo function Write-Line([string]$text^) {
echo     if ([string]::IsNullOrWhiteSpace($text^)^) {
echo         Write-Output ''
echo     } else {
echo         Write-Output $text.Trim(^)
echo     }
echo }
echo $proc = Get-CimInstance -ClassName Win32_Processor ^| Select-Object -First 1
echo Write-Line $proc.ProcessorId
echo $prod = Get-CimInstance -ClassName Win32_ComputerSystemProduct ^| Select-Object -First 1
echo Write-Line $prod.UUID
echo $bios = Get-CimInstance -ClassName Win32_BIOS ^| Select-Object -First 1
echo Write-Line $bios.SerialNumber
echo $board = Get-CimInstance -ClassName Win32_BaseBoard ^| Select-Object -First 1
echo Write-Line $board.SerialNumber
echo $disk = Get-CimInstance -ClassName Win32_LogicalDisk -Filter "DeviceID='C:'" ^| Select-Object -First 1
echo Write-Line $disk.VolumeSerialNumber
echo $nic = Get-CimInstance -ClassName Win32_NetworkAdapterConfiguration -Filter 'IPEnabled=True' ^| Select-Object -First 1
echo if ($nic -and $nic.MACAddress^) {
echo     Write-Output (($nic.MACAddress -replace ':', '-'^).ToUpperInvariant(^)^)
echo } else {
echo     Write-Output ''
echo }
) > "%SCRIPT_PATH%"

powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_PATH%"
del /q "%SCRIPT_PATH%"

pause
