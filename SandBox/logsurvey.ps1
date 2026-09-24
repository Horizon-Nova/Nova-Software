# 可反查的紀錄來源普查：每個來源在非提權下讀不讀得到、有幾筆、涵蓋多久。
# 「帳號被盜要反查誰做了什麼」的第一步是知道機器上到底留了什麼。

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = 'SilentlyContinue'

$logs = @(
  'Security',
  'System',
  'Application',
  'Microsoft-Windows-TerminalServices-LocalSessionManager/Operational',
  'Microsoft-Windows-TerminalServices-RemoteConnectionManager/Operational',
  'Microsoft-Windows-RemoteDesktopServices-RdpCoreTS/Operational',
  'Microsoft-Windows-WinRM/Operational',
  'Microsoft-Windows-PowerShell/Operational',
  'Windows PowerShell',
  'Microsoft-Windows-TaskScheduler/Operational',
  'Microsoft-Windows-WMI-Activity/Operational',
  'Microsoft-Windows-Windows Defender/Operational',
  'Microsoft-Windows-CodeIntegrity/Operational',
  'Microsoft-Windows-DriverFrameworks-UserMode/Operational',
  'Microsoft-Windows-Bits-Client/Operational',
  'Microsoft-Windows-DNS-Client/Operational',
  'Microsoft-Windows-SmbClient/Security',
  'Microsoft-Windows-SmbServer/Operational',
  'Microsoft-Windows-User Profile Service/Operational',
  'Microsoft-Windows-Windows Firewall With Advanced Security/Firewall',
  'Microsoft-Windows-NetworkProfile/Operational',
  'Microsoft-Windows-Kernel-PnP/Configuration',
  'Microsoft-Windows-Storage-Storport/Operational',
  'Microsoft-Windows-Winlogon/Operational',
  'Microsoft-Windows-Shell-Core/Operational',
  'Microsoft-Windows-AppLocker/EXE and DLL',
  'Microsoft-Windows-Sysmon/Operational',
  'Microsoft-Windows-Security-Mitigations/KernelMode',
  'Microsoft-Windows-Authentication/AuthenticationPolicyFailures-DomainController',
  'Microsoft-Windows-LSA/Operational',
  'Microsoft-Windows-Diagnostics-Performance/Operational'
)

Write-Output "########## 事件記錄檔：非提權可讀性"
Write-Output ("{0,-72} {1,>9} {2,>10}  {3}" -f '記錄檔', '筆數', '可讀', '最舊 → 最新')
Write-Output ('-' * 130)
foreach ($name in $logs) {
  $info = Get-WinEvent -ListLog $name -ErrorAction SilentlyContinue
  if (-not $info) { Write-Output ("{0,-72} {1,>9}  不存在" -f $name, '—'); continue }
  $oldest = Get-WinEvent -LogName $name -Oldest -MaxEvents 1 -ErrorAction SilentlyContinue
  $newest = Get-WinEvent -LogName $name -MaxEvents 1 -ErrorAction SilentlyContinue
  $readable = if ($newest -or $info.RecordCount -eq 0) { '可讀' } else { '拒絕' }
  $span = if ($oldest -and $newest) { "$($oldest.TimeCreated) → $($newest.TimeCreated)" } else { '' }
  Write-Output ("{0,-72} {1,>9} {2,>10}  {3}" -f $name, $info.RecordCount, $readable, $span)
}

Write-Output ""
Write-Output "########## 記錄檔的保留設定（滿了會覆蓋最舊的，涵蓋期間由此決定）"
foreach ($name in 'Security','System','Application','Microsoft-Windows-TerminalServices-LocalSessionManager/Operational') {
  $i = Get-WinEvent -ListLog $name -ErrorAction SilentlyContinue
  if ($i) {
    Write-Output ("  {0}" -f $name)
    Write-Output ("      最大 {0:N0} 位元組  目前 {1:N0}  筆數 {2:N0}  模式 {3}  啟用 {4}" -f `
      $i.MaximumSizeInBytes, $i.FileSize, $i.RecordCount, $i.LogMode, $i.IsEnabled)
  }
}

Write-Output ""
Write-Output "########## 稽核原則：哪些事件根本沒被記錄"
auditpol /get /category:* 2>&1 | Select-Object -First 40
