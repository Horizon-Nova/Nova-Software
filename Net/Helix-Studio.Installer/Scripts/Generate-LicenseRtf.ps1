# Convert plain-text license content into WiX-friendly RTF.
param(
    [Parameter(ParameterSetName = 'Single', Mandatory = $true)]
    [string] $SourcePath,
    [Parameter(ParameterSetName = 'Single', Mandatory = $true)]
    [string] $DestPath,
    [Parameter(ParameterSetName = 'Single')]
    [int] $LangId = 1028,
    [Parameter(ParameterSetName = 'All')]
    [switch] $All,
    [Parameter(ParameterSetName = 'All')]
    [string] $InstallerDir = ''
)

function Convert-ToLicenseRtf {
    param(
        [string] $InPath,
        [string] $OutPath,
        [int] $RtfLangId
    )

    if (-not (Test-Path -LiteralPath $InPath)) {
        Write-Error "[error] Source file not found: $InPath"
        exit 1
    }

    function ConvertTo-RtfText {
        param([string] $Value)

        $local = New-Object System.Text.StringBuilder
        foreach ($ch in $Value.ToCharArray()) {
            if ($ch -eq '\') {
                [void]$local.Append('\\')
                continue
            }
            if ($ch -eq '{') {
                [void]$local.Append('\{')
                continue
            }
            if ($ch -eq '}') {
                [void]$local.Append('\}')
                continue
            }
            $code = [int]$ch
            if ($code -lt 128) {
                [void]$local.Append($ch)
                continue
            }
            if ($code -gt 32767) {
                $code = $code - 65536
            }
            [void]$local.AppendFormat('\u{0}?', $code)
        }
        return $local.ToString()
    }

    $text = [System.IO.File]::ReadAllText($InPath, [System.Text.UTF8Encoding]::new($false))
    $lines = $text -split "\r?\n"
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append("{\rtf1\ansi\ansicpg65001\deff0")
    [void]$sb.Append("{\fonttbl{\f0\fswiss Segoe UI;}}")
    [void]$sb.Append('\viewkind4\uc1')

    for ($index = 0; $index -lt $lines.Length; $index++) {
        $line = $lines[$index].TrimEnd()

        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $escaped = ConvertTo-RtfText $line.Trim()

        if ($index -eq 0) {
            [void]$sb.AppendFormat('\pard\qc\sb120\sa180\lang{0}\f0\b\fs28 {1}\b0\fs22\par ', $RtfLangId, $escaped)
            continue
        }

        if (
            ($line.Contains(':') -or $line.Contains([char]0xFF1A)) -and
            $index -le 2
        ) {
            [void]$sb.AppendFormat('\pard\sa60\lang{0}\f0\fs18 {1}\par ', $RtfLangId, $escaped)
            continue
        }

        if (
            ($line.EndsWith(':') -or $line.EndsWith([string][char]0xFF1A)) -and
            $line.Length -le 24
        ) {
            [void]$sb.AppendFormat('\pard\sb120\sa60\lang{0}\f0\b\fs20 {1}\b0\fs22\par ', $RtfLangId, $escaped)
            continue
        }

        if ($line -match '^\d+\.\s+[A-Z]' -or $line -match '^[^ -~]+ .+') {
            [void]$sb.AppendFormat('\pard\sb120\sa60\lang{0}\f0\b\fs22 {1}\b0\par ', $RtfLangId, $escaped)
            continue
        }

        if ($line -match '^\(\d+\)') {
            [void]$sb.AppendFormat('\pard\li720\fi-240\sa30\lang{0}\f0\fs20 {1}\par ', $RtfLangId, $escaped)
            continue
        }

        if ($line -match '^\d+\.\s') {
            [void]$sb.AppendFormat('\pard\li360\fi-240\sa30\lang{0}\f0\fs20 {1}\par ', $RtfLangId, $escaped)
            continue
        }

        [void]$sb.AppendFormat('\pard\sa60\lang{0}\f0\fs20 {1}\par ', $RtfLangId, $escaped)
    }

    [void]$sb.Append('}')
    $dir = [System.IO.Path]::GetDirectoryName($OutPath)
    if (-not [string]::IsNullOrEmpty($dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    [System.IO.File]::WriteAllText($OutPath, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
    Write-Host "[ok] Generated: $OutPath"
}

if ($All) {
    if ([string]::IsNullOrWhiteSpace($InstallerDir)) {
        $InstallerDir = Split-Path -Parent $PSScriptRoot
    }
    $legal = Join-Path $InstallerDir '..\Helix-Studio\Assets\Legal'
    Convert-ToLicenseRtf -InPath (Join-Path $legal 'Helix-Studio-Terms.zh-Hant.txt') -OutPath (Join-Path $InstallerDir 'Assets\License.zh-TW.rtf') -RtfLangId 1028
    Convert-ToLicenseRtf -InPath (Join-Path $legal 'Helix-Studio-Terms.en.txt') -OutPath (Join-Path $InstallerDir 'Assets\License.en-US.rtf') -RtfLangId 1033
    exit 0
}

Convert-ToLicenseRtf -InPath $SourcePath -OutPath $DestPath -RtfLangId $LangId
