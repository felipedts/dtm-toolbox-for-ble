<#
.SYNOPSIS
Builds the release executable and copies it to dist\.
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

dotnet build (Join-Path $root 'DtmToolbox.sln') -c Release
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
Copy-Item (Join-Path $root 'src\DtmToolbox\bin\Release\net462\DtmToolbox.exe') $dist -Force
Write-Host "Output: $(Join-Path $dist 'DtmToolbox.exe')"
