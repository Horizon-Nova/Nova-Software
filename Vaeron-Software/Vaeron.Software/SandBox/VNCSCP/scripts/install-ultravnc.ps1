# Put UltraVNC on one VM-Lab machine: copy the installers into C:\test, install silently, report what the machine says.
#   -Role Server : UltraVNC Server (installed and started as the service uvnc_service) + Viewer, VNC password from vendor\test.secret
#   -Role Client : UltraVNC Viewer only
# The VM must exist (VM-Lab\new-vm.ps1); it is started if it is off and left running (checkpoint and shutdown are done by the caller).
# Usage:  powershell -ExecutionPolicy Bypass -File install-ultravnc.ps1 -Vm VNC-Server -Role Server
param(
    [Parameter(Mandatory = $true)][string]$Vm,
    [Parameter(Mandatory = $true)][ValidateSet('Server', 'Client')][string]$Role,
    [string]$Setup = 'UltraVNC_1830_X64_Setup.exe',
    [string]$Lab
)
$ErrorActionPreference = 'Stop'
$here   = Split-Path -Parent $MyInvocation.MyCommand.Path
$vendor = Join-Path (Split-Path -Parent $here) 'vendor'
if (-not $Lab) { $Lab = [IO.Path]::GetFullPath((Join-Path $here '..\..\..\..\..\..\VM-Lab')) }
$credential = New-Object PSCredential('tester', (ConvertTo-SecureString (Get-Content (Join-Path $Lab 'tester.secret') -Raw) -AsPlainText -Force))

# The VM running and reachable through PowerShell Direct.
if ((Get-VM -Name $Vm).State -ne 'Running') { Start-VM -Name $Vm }
$session = $null
$started = Get-Date
while (-not $session -and ((Get-Date) - $started).TotalMinutes -lt 10) {
    Start-Sleep 5
    $session = New-PSSession -VMName $Vm -Credential $credential -ErrorAction SilentlyContinue
}
if (-not $session) { throw "$Vm did not come up" }

# Every downloaded installer goes to C:\test (the older ones too, for later comparisons).
Invoke-Command $session { New-Item C:\test -ItemType Directory -Force | Out-Null }
Get-ChildItem $vendor -Recurse -Include *.exe, *.zip | ForEach-Object {
    Copy-Item $_.FullName -Destination "C:\test\$($_.Name)" -ToSession $session
}

# Silent install (Inno Setup). Components and tasks are the names in UltraVNC_installer_x64.iss;
# /setpasswd= is handed to setpasswd.exe by the installer ([Run] {param:setpasswd|}); the view-only password stays random.
$arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/LOG=C:\test\install.log')
if ($Role -eq 'Server') {
    $password = (Get-Content (Join-Path $vendor 'test.secret') -Raw).Trim()
    $arguments += '/TYPE=custom', '/COMPONENTS=UltraVNC_Server,UltraVNC_Viewer', '/TASKS=installservice,startservice', "/setpasswd=$password"
} else {
    $arguments += '/TYPE=custom', '/COMPONENTS=UltraVNC_Viewer', '/TASKS='
}
$code = Invoke-Command $session {
    param($file, $arguments)
    (Start-Process "C:\test\$file" -ArgumentList $arguments -Wait -PassThru).ExitCode
} -ArgumentList $Setup, $arguments

# Inno Setup writes its whole command line into the log, /setpasswd= included: mask it in the VM's copy before anything else reads it.
if ($Role -eq 'Server') {
    Invoke-Command $session {
        param($secret)
        $text = [IO.File]::ReadAllText('C:\test\install.log')
        [IO.File]::WriteAllText('C:\test\install.log', $text.Replace($secret, '********'))
    } -ArgumentList $password
}

# What the machine says now.
$state = Invoke-Command $session {
    $entry = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*' -ErrorAction SilentlyContinue | Where-Object DisplayName -like 'UltraVNC*' | Select-Object -First 1
    $svc = Get-CimInstance Win32_Service -Filter "Name='uvnc_service'"
    $ini = @("$env:ProgramData\UltraVNC\ultravnc.ini", "$($entry.InstallLocation)ultravnc.ini") | Where-Object { $_ -and (Test-Path $_) }
    [pscustomobject]@{
        Product   = "$($entry.DisplayName) $($entry.DisplayVersion)"
        Location  = "$($entry.InstallLocation)"
        Files     = (@(Get-ChildItem $entry.InstallLocation -Filter *.exe -ErrorAction SilentlyContinue | ForEach-Object Name) -join ', ')
        Service   = "$($svc.State) $($svc.StartMode) $($svc.PathName)"
        Listening = (@(Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object LocalPort -in 5900, 5800 | ForEach-Object { "$($_.LocalAddress):$($_.LocalPort)" }) -join ', ')
        Ini       = ($ini -join ', ')
        Password  = [bool]($ini | Where-Object { (Get-Content $_) -match '^passwd=' })
        Firewall  = (@(Get-NetFirewallRule -DisplayName 'UltraVNC*' -ErrorAction SilentlyContinue | ForEach-Object { "$($_.DisplayName) $($_.Direction) $($_.Enabled)" }) -join '; ')
        Address   = (@(Get-NetIPAddress -AddressFamily IPv4 | Where-Object IPAddress -notlike '127.*' | ForEach-Object IPAddress) -join ', ')
    }
}
Copy-Item 'C:\test\install.log' -Destination (Join-Path $vendor "install-$Vm.log") -FromSession $session
Remove-PSSession $session
Write-Host "exit code: $code"
$state | Select-Object Product, Location, Files, Service, Listening, Ini, Password, Firewall, Address | Format-List
