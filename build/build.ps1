<#
.SYNOPSIS
Builds, tests and packs a release into artifacts\.

.DESCRIPTION
Output:
  artifacts\DtmToolbox-<version>-setup.exe      installer, carries the .NET Framework 4.8 offline installer
  artifacts\DtmToolbox-<version>-portable.zip   the executable alone, runs from any folder
  artifacts\SHA256SUMS.txt

The executable itself is built in the usual place, src\DtmToolbox\bin\Release\net462.
Needs the .NET SDK 8.0 and, for the installer, Inno Setup 6 (build\install-innosetup.ps1).

.PARAMETER Version
Version to stamp, for example 1.0.0. Default: the version in Directory.Build.props.

.PARAMETER SkipInstaller
Builds the portable archive only. No Inno Setup and no .NET Framework download needed.
#>
param(
    [string]$Version,
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

function Invoke-Checked([string]$what, [scriptblock]$command) {
    & $command
    if ($LASTEXITCODE -ne 0) {
        throw "$what failed with exit code $LASTEXITCODE."
    }
}

if (-not $Version) {
    [xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
    $Version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
}

Write-Host "== DTM Toolbox for BLE $Version"
$solution = Join-Path $root 'DtmToolbox.sln'
Invoke-Checked 'Build' { dotnet build $solution -c Release "-p:Version=$Version" --nologo }
Invoke-Checked 'Tests' { dotnet test $solution -c Release --no-build --nologo }

$exe = Join-Path $root 'src\DtmToolbox\bin\Release\net462\DtmToolbox.exe'
$artifacts = Join-Path $root 'artifacts'
if (Test-Path $artifacts) {
    Remove-Item $artifacts -Recurse -Force
}

New-Item -ItemType Directory -Force $artifacts | Out-Null

# Portable archive: the executable, the license and the quick start.
$staging = Join-Path $artifacts 'portable'
New-Item -ItemType Directory -Force $staging | Out-Null
Copy-Item $exe $staging
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $staging 'LICENSE.txt')
Copy-Item (Join-Path $root 'docs\quick-start.md') (Join-Path $staging 'Quick start.txt')
$portable = Join-Path $artifacts "DtmToolbox-$Version-portable.zip"
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $portable
Remove-Item $staging -Recurse -Force

if (-not $SkipInstaller) {
    & (Join-Path $PSScriptRoot 'fetch-redist.ps1')

    $compiler = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $compiler) {
        throw 'Inno Setup 6 was not found. Run build\install-innosetup.ps1 first.'
    }

    Invoke-Checked 'Installer' { & $compiler /Qp "/DAppVersion=$Version" (Join-Path $root 'installer\DtmToolbox.iss') }
}

# Checksums in the format sha256sum reads.
$sums = Get-ChildItem $artifacts -File | Sort-Object Name | ForEach-Object {
    (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name
}
Set-Content -Path (Join-Path $artifacts 'SHA256SUMS.txt') -Value $sums -Encoding ascii

Write-Host ''
Get-ChildItem $artifacts -File | ForEach-Object { Write-Host ("{0,12:N0}  {1}" -f $_.Length, $_.Name) }
