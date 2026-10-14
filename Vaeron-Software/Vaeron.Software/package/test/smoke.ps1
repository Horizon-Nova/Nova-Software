# Install test for one edition of one product on a clean test VM (no network): install, check, open the platform, reinstall over it, uninstall.
# Every step reports what the machine itself says (exit codes, the service, the database, the processes); a failed check is written down and the run goes on,
# so one run shows everything that is wrong. The VM is returned to its clean checkpoint at the end.
# Usage:  powershell -ExecutionPolicy Bypass -File smoke.ps1 -Product HelixStudio -Edition Win10-11-x64 -Vm HelixTest-Win11
param(
    [Parameter(Mandatory = $true)][string]$Product,
    [Parameter(Mandatory = $true)][string]$Edition,
    [Parameter(Mandatory = $true)][string]$Vm,
    [string]$Lab
)
# The VM is a machine from VM-Lab (new-vm.ps1): checkpoint "clean" with "tester" signed in, the password in VM-Lab\tester.secret.
# VM-Lab sits next to this repository's folder (…\Nova-Software and …\VM-Lab); -Lab points elsewhere.
$ErrorActionPreference = 'Stop'
$here   = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Lab) { $Lab = [IO.Path]::GetFullPath((Join-Path $here '..\..\..\..\..\VM-Lab')) }
if (-not (Test-Path (Join-Path $Lab 'tester.secret'))) { throw "no VM-Lab at $Lab (put VM-Lab next to the repository folder, or pass -Lab)" }
$sln    = Split-Path -Parent (Split-Path -Parent $here)
$p      = Get-Content (Join-Path $here "..\products\$Product.json") -Raw -Encoding UTF8 | ConvertFrom-Json
$e      = $p.editions | Where-Object { $_.label -eq $Edition }
if (-not $e) { throw "$Product has no edition $Edition" }
$setup  = Get-ChildItem (Join-Path $sln 'out\release') -Recurse -Filter "$($p.name)-*-$Edition-Setup.exe" | Where-Object { $_.Name -match "^$([regex]::Escape($p.name))-\d" } | Select-Object -First 1
if (-not $setup) { throw "no installer for $Product $Edition in out\release (run dotnet run --project dev\Release -- $Product first)" }
$report = Join-Path $sln "out\test\smoke-$($p.name)-$Edition-$(Get-Date -Format yyyyMMdd-HHmmss)"
New-Item $report -ItemType Directory -Force | Out-Null
$credential = New-Object PSCredential('tester', (ConvertTo-SecureString (Get-Content (Join-Path $Lab 'tester.secret') -Raw) -AsPlainText -Force))
$results = New-Object Collections.Generic.List[object]

# One line in the report: what was checked, what the machine said, whether it is what it should be.
function Note($step, $check, $said, $pass) {
    $results.Add([pscustomobject]@{ Step = $step; Check = $check; Said = "$said"; Pass = [bool]$pass })
    Write-Host ("[{0}] {1} / {2}: {3}" -f $(if ($pass) { 'OK' } else { 'FAIL' }), $step, $check, $said)
}

# The VM's screen, saved as a picture next to the report (Hyper-V hands back at most 640 x 480, 16-bit).
function Screen($name) {
    $service = Get-CimInstance -Namespace root\virtualization\v2 -ClassName Msvm_VirtualSystemManagementService
    $settings = Get-CimInstance -Namespace root\virtualization\v2 -ClassName Msvm_VirtualSystemSettingData -Filter "ElementName='$Vm' and VirtualSystemType='Microsoft:Hyper-V:System:Realized'"
    $shot = Invoke-CimMethod -InputObject $service -MethodName GetVirtualSystemThumbnailImage -Arguments @{ TargetSystem = $settings; WidthPixels = [uint16]640; HeightPixels = [uint16]480 }
    $bytes = [byte[]]$shot.ImageData
    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object Drawing.Bitmap 640, 480, ([Drawing.Imaging.PixelFormat]::Format16bppRgb565)
    $data = $bmp.LockBits((New-Object Drawing.Rectangle 0, 0, 640, 480), [Drawing.Imaging.ImageLockMode]::WriteOnly, $bmp.PixelFormat)
    [Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, [Math]::Min($bytes.Length, $data.Stride * 480))
    $bmp.UnlockBits($data)
    $bmp.Save((Join-Path $report "$name.png"))
}

# What the machine looks like right now: installed entry, database service, listening port, the database itself, the platform's settings file.
$probe = {
    param($p, $e)
    $keys = "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{$($p.appId)}_is1", "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{$($p.appId)}_is1"
    $entries = @($keys | Where-Object { Test-Path $_ } | ForEach-Object { Get-ItemProperty $_ })
    $svc = Get-CimInstance Win32_Service -Filter "Name='$($p.service)'"
    $db = Join-Path $env:ProgramData "$($p.dataFolder)\db\$($e.pg)"
    $state = [ordered]@{
        Entries   = $entries.Count
        Location  = "$($entries | Select-Object -First 1 -ExpandProperty InstallLocation)"
        Version   = "$($entries | Select-Object -First 1 -ExpandProperty DisplayVersion)"
        Service   = "$($svc.State)"
        Listening = (@(Get-NetTCPConnection -LocalPort $p.port -State Listen -ErrorAction SilentlyContinue | ForEach-Object { $_.LocalAddress }) -join ',')
        Plaintext = (Test-Path "$env:ProgramData\$($p.dataFolder)\appsettings.json") -and (Get-Content "$env:ProgramData\$($p.dataFolder)\appsettings.json" -Raw).Contains('Password')
        DataKept  = Test-Path (Join-Path $db 'pgdata\PG_VERSION')
        Tables    = ''; Tools = ''; Errors = ''
    }
    if ($svc.State -eq 'Running' -and $entries.Count -gt 0) {
        $psql = Join-Path $entries[0].InstallLocation 'pgsql\bin\psql.exe'
        $env:PGPASSWORD = (Get-Content (Join-Path $db 'db.key') -Raw).Trim()
        $env:PGCLIENTENCODING = 'UTF8'
        $q = { param($sql) $null | & $psql -w -tA -h localhost -p $p.port -U helix -d helix_studio -c $sql 2>&1 }
        $state.Tables = "$(& $q "select count(*) filter (where table_type='BASE TABLE') || ' tables, ' || count(*) filter (where table_type='VIEW') || ' views' from information_schema.tables where table_schema='dbo'")"
        $state.Tools  = "$(& $q "select coalesce(string_agg(tool_no || ' ' || version, ', '), '') from dbo.tool where type='tool'")"
        $state.Errors = "$(& $q "select count(*) || ' ' || coalesce(string_agg(left(message, 80), ' | '), '') from dbo.event_log where type='error'")"
    }
    [pscustomobject]$state
}

# The VM at its clean checkpoint, running, reachable through PowerShell Direct.
Restore-VMCheckpoint -VMName $Vm -Name 'clean' -Confirm:$false
Start-VM -Name $Vm
$session = $null
$started = Get-Date
while (-not $session -and ((Get-Date) - $started).TotalMinutes -lt 10) {
    Start-Sleep 5
    $session = New-PSSession -VMName $Vm -Credential $credential -ErrorAction SilentlyContinue
}
if (-not $session) { throw "$Vm did not come up" }
Invoke-Command $session { New-Item C:\test -ItemType Directory -Force | Out-Null }
Copy-Item $setup.FullName -Destination "C:\test\$($setup.Name)" -ToSession $session

# Run the installer on the signed-in operator desktop, elevated, the way an IT person double-clicks it there; returns its exit code.
# (Run from this remote session instead, Windows will not let it close a platform open on the desktop, and the update aborts.)
$runSetup = {
    param($file, $log)
    $action = New-ScheduledTaskAction -Execute "C:\test\$file" -Argument "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=$log"
    $principal = New-ScheduledTaskPrincipal -UserId 'tester' -LogonType Interactive -RunLevel Highest
    Register-ScheduledTask -TaskName 'run-setup' -Action $action -Principal $principal -Force | Out-Null
    Start-ScheduledTask -TaskName 'run-setup'
    Start-Sleep 3
    while ((Get-ScheduledTask -TaskName 'run-setup').State -eq 'Running') { Start-Sleep 2 }
    (Get-ScheduledTaskInfo -TaskName 'run-setup').LastTaskResult
}

# Install: silent; the installer's own log comes back with the report.
$code = Invoke-Command $session $runSetup -ArgumentList $setup.Name, 'C:\test\install.log'
Copy-Item 'C:\test\install.log' -Destination (Join-Path $report 'install.log') -FromSession $session
$s = Invoke-Command $session $probe -ArgumentList $p, $e
Note 'install' 'exit code' $code ($code -eq 0)
Note 'install' 'one installed entry' "$($s.Entries) at $($s.Location)" ($s.Entries -eq 1)
Note 'install' 'database service' $s.Service ($s.Service -eq 'Running')
Note 'install' 'listens on this machine only' $s.Listening ($s.Listening -and ($s.Listening -split ',' | Where-Object { $_ -notin '127.0.0.1', '::1' }).Count -eq 0)
Note 'install' 'tables and views' $s.Tables ($s.Tables -match '^\d+ tables, \d+ views$' -and $s.Tables -notmatch '^0 ')

# Open the platform as the signed-in operator (a scheduled task runs it on the operator's desktop, not in this remote session).
Invoke-Command $session {
    param($location)
    $exe = Join-Path $location 'HelixStudio.exe'
    $action = New-ScheduledTaskAction -Execute $exe -WorkingDirectory $location
    $principal = New-ScheduledTaskPrincipal -UserId 'tester' -LogonType Interactive -RunLevel Limited
    Register-ScheduledTask -TaskName 'open-platform' -Action $action -Principal $principal -Force | Out-Null
    Start-ScheduledTask -TaskName 'open-platform'
} -ArgumentList $s.Location
Start-Sleep 25
Screen 'platform'
$alive = Invoke-Command $session { @(Get-Process HelixStudio -ErrorAction SilentlyContinue).Count }
$s = Invoke-Command $session $probe -ArgumentList $p, $e
Note 'open' 'platform is running' "$alive process" ($alive -ge 1)
Note 'open' 'password moved out of the settings file' "plaintext left: $($s.Plaintext)" (-not $s.Plaintext)
Note 'open' 'tools on this machine' $s.Tools ((-not $p.toolsInInstaller) -or $s.Tools.Length -gt 0)
Note 'open' 'errors logged' $s.Errors ($s.Errors -match '^0 ')

# Reinstall over the running platform (same as an update): data stays, service comes back.
$code = Invoke-Command $session $runSetup -ArgumentList $setup.Name, 'C:\test\update.log'
Copy-Item 'C:\test\update.log' -Destination (Join-Path $report 'update.log') -FromSession $session
$before = $s.Tools
$s = Invoke-Command $session $probe -ArgumentList $p, $e
Note 'update' 'exit code' $code ($code -eq 0)
Note 'update' 'one installed entry' $s.Entries ($s.Entries -eq 1)
Note 'update' 'database service' $s.Service ($s.Service -eq 'Running')
Note 'update' 'data kept' "tools before: $before / after: $($s.Tools)" ($s.Tools -eq $before)

# Uninstall: the entry, the service and the program folder go; the database stays for a later reinstall.
$code = Invoke-Command $session {
    param($location)
    Get-Process HelixStudio -ErrorAction SilentlyContinue | Stop-Process -Force
    (Start-Process (Join-Path $location 'unins000.exe') -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait -PassThru).ExitCode
} -ArgumentList $s.Location
Start-Sleep 5
$left = Invoke-Command $session { param($location) Test-Path $location } -ArgumentList $s.Location
$s = Invoke-Command $session $probe -ArgumentList $p, $e
Note 'uninstall' 'exit code' $code ($code -eq 0)
Note 'uninstall' 'installed entry gone' $s.Entries ($s.Entries -eq 0)
Note 'uninstall' 'database service gone' "'$($s.Service)'" ($s.Service -eq '')
Note 'uninstall' 'program folder gone' "left: $left" (-not $left)
Note 'uninstall' 'database kept' "kept: $($s.DataKept)" $s.DataKept

# Back to clean; the report stays.
Remove-PSSession $session
Stop-VM -Name $Vm -TurnOff -Force
Restore-VMCheckpoint -VMName $Vm -Name 'clean' -Confirm:$false
$results | Export-Csv (Join-Path $report 'results.csv') -NoTypeInformation -Encoding UTF8
$failed = @($results | Where-Object { -not $_.Pass }).Count
Write-Host "$(if ($failed) { 'FAIL' } else { 'OK' }): $($results.Count - $failed)/$($results.Count) checks passed - $report"
exit $failed
