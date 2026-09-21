<#
.SYNOPSIS
Downloads the .NET Framework 4.8 offline installer that Setup carries, into installer\redist.

.DESCRIPTION
The file comes from the Microsoft download link and is accepted only with a valid Authenticode
signature of Microsoft Corporation. It stays out of the repository: it is larger than the file
size limit of GitHub. A file already in place with a valid signature is kept.
#>
param(
    [string]$Destination = (Join-Path $PSScriptRoot '..\installer\redist')
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$url = 'https://go.microsoft.com/fwlink/?linkid=2088631'
$fileName = 'ndp48-x86-x64-allos-enu.exe'

function Test-MicrosoftSignature([string]$path) {
    $signature = Get-AuthenticodeSignature $path
    return $signature.Status -eq 'Valid' -and $signature.SignerCertificate.Subject -match 'O=Microsoft Corporation'
}

$folder = [System.IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Force $folder | Out-Null
$file = Join-Path $folder $fileName

if ((Test-Path $file) -and (Test-MicrosoftSignature $file)) {
    Write-Host "Already in place: $file"
    return
}

Write-Host "Downloading $fileName ..."
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Invoke-WebRequest -Uri $url -OutFile $file -UseBasicParsing

if (-not (Test-MicrosoftSignature $file)) {
    Remove-Item $file -Force
    throw "The downloaded file does not carry a valid Microsoft signature. It was deleted."
}

$size = [math]::Round((Get-Item $file).Length / 1MB, 1)
Write-Host "Downloaded $file ($size MB), SHA-256 $((Get-FileHash $file -Algorithm SHA256).Hash)"
