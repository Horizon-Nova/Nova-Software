# build.ps1｜以 global.json 釘住的 SDK 版本建置，接著依組態產生散佈物。
# 機器上沒有該版本帶時，把 SDK 裝進本 repo 的 .dotnet\，不寫入 Program Files、不改 PATH。
param([string]$Configuration = 'Debug')

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$root = $PSScriptRoot
$required = (Get-Content (Join-Path $root 'global.json') -Raw -Encoding UTF8 | ConvertFrom-Json).sdk.version
$band = $required.Split('.')[0]
$localRoot = Join-Path $root '.dotnet'
$localDotnet = Join-Path $localRoot 'dotnet.exe'

# 機器上與 global.json 同一版本帶的 SDK。有就直接用，這段之後不動作。
$machineSdks = @(& dotnet --list-sdks 2>$null) | Where-Object { $_ -like "$band.*" }

$missing = @($machineSdks).Count -eq 0 -and -not (Test-Path $localDotnet)

@($missing) | Where-Object { $_ } | ForEach-Object {
    Write-Host "未偵測到 .NET $band SDK，安裝 $required 至 $localRoot（不影響系統既有安裝）"
    $installer = Join-Path ([System.IO.Path]::GetTempPath()) 'dotnet-install.ps1'
    Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -UseBasicParsing
    & $installer -Version $required -InstallDir $localRoot -NoPath
}

# 區域安裝優先；沒有才用機器上的。
$dotnet = @(@($localDotnet) | Where-Object { Test-Path $_ }) + @('dotnet') | Select-Object -First 1

& $dotnet build (Join-Path $root 'Vaeron.HelixStudio.slnx') -c $Configuration --nologo @args

# 產生散佈物。要產出什麼由 Build\Package.$Configuration.props 的清單決定；Debug 的清單是空的。
& $dotnet msbuild (Join-Path $root 'Build\Package.proj') -p:Configuration=$Configuration -nologo

exit $LASTEXITCODE
