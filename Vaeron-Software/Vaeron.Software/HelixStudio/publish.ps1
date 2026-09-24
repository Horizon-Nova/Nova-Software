# Publish: build -> obfuscate -> overwrite HelixStudio.dll in the publish output.
# Usage (from the HelixStudio folder):  powershell -ExecutionPolicy Bypass -File publish.ps1
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$sln  = Split-Path -Parent $here
$dist = Join-Path $here 'dist'

# A running instance locks the output files; stop it first.
Get-Process HelixStudio -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 2

# Framework-dependent publish (client already has the .NET 10 Desktop runtime).
Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish (Join-Path $here 'HelixStudio.csproj') -c Release -o $dist --nologo
if ($LASTEXITCODE) { throw 'publish failed' }

# Obfuscate the main assembly -> bin\Obfuscated\HelixStudio.dll
Push-Location $sln
dotnet obfuscar.console (Join-Path $here 'obfuscar.xml')
$rc = $LASTEXITCODE
Pop-Location
if ($rc) { throw 'obfuscar failed' }

# Replace the un-obfuscated dll in the publish output with the obfuscated one.
$obfDll  = Join-Path $here 'bin\Obfuscated\HelixStudio.dll'
$distDll = Join-Path $dist 'HelixStudio.dll'
Copy-Item $obfDll $distDll -Force

# The pdb leaks original names; do not ship it.
Remove-Item (Join-Path $dist 'HelixStudio.pdb') -Force -ErrorAction SilentlyContinue

# Gate: dist dll must equal the obfuscated one, else the copy did not take (e.g. file locked).
if ((Get-Item $distDll).Length -ne (Get-Item $obfDll).Length) {
    throw 'dist HelixStudio.dll is not the obfuscated build (locked by a running process?) - close HelixStudio and re-run'
}
$bytes = [IO.File]::ReadAllBytes($distDll)
foreach ($name in 'LicenceStore','Anchors','PackageStore','DeviceStore','SecretStore') {
    $pat = [Text.Encoding]::UTF8.GetBytes($name)
    for ($i=0; $i -le $bytes.Length-$pat.Length; $i++) {
        $m=$true; for ($j=0;$j -lt $pat.Length;$j++){ if($bytes[$i+$j]-ne$pat[$j]){$m=$false;break} }
        if ($m) { throw "dist still contains un-obfuscated type name '$name'" }
    }
}

Write-Host "OK: $dist (verified obfuscated)"
