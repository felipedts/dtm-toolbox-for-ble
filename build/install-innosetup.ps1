<#
.SYNOPSIS
Installs the Inno Setup version the installer script is written for.

.DESCRIPTION
Downloads the installer from the releases of the Inno Setup project, checks its SHA-256 and its
Authenticode signature, and runs it silently. Without -AllUsers it installs for the current user,
which needs no administrator rights.
#>
param(
    [switch]$AllUsers
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$version = '6.7.3'
$url = "https://github.com/jrsoftware/issrc/releases/download/is-$($version -replace '\.', '_')/innosetup-$version.exe"
$sha256 = '9C73C3BAE7ED48D44112A0F48E66742C00090BDB5BEF71D9D3C056C66E97B732'

$file = Join-Path ([System.IO.Path]::GetTempPath()) "innosetup-$version.exe"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Invoke-WebRequest -Uri $url -OutFile $file -UseBasicParsing

$hash = (Get-FileHash $file -Algorithm SHA256).Hash
if ($hash -ne $sha256) {
    throw "Unexpected SHA-256 for innosetup-$version.exe: $hash"
}

if ((Get-AuthenticodeSignature $file).Status -ne 'Valid') {
    throw "innosetup-$version.exe does not carry a valid signature."
}

$scope = if ($AllUsers) { '/ALLUSERS' } else { '/CURRENTUSER' }
$process = Start-Process -FilePath $file -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', $scope -Wait -PassThru
if ($process.ExitCode -ne 0) {
    throw "The Inno Setup installer ended with code $($process.ExitCode)."
}

Write-Host "Inno Setup $version installed."
