<#
.SYNOPSIS
Renders the application icon and packs it into src\DtmToolbox\Resources\app.ico.

.DESCRIPTION
The icon is drawn with GDI+ at every size the shell uses (16 to 256 px) and stored as
PNG-compressed entries, which Windows Vista and later read directly. This script is the
only source of the artwork; run it again after changing the geometry or the colors.

.PARAMETER Output
Path of the .ico file to write.

.PARAMETER PreviewPng
Optional path of a 256 px PNG rendering, for a quick look at the result.
#>
param(
    [string]$Output = (Join-Path $PSScriptRoot '..\src\DtmToolbox\Resources\app.ico'),
    [string]$PreviewPng
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256

# Geometry on a 256 px canvas: rounded square background, one dot and three arcs above it.
$cornerRadius = 56
$accent = [System.Drawing.Color]::FromArgb(255, 0x00, 0xAC, 0xC1)
$centerX = 128
$centerY = 184
$arcRadii = 52, 92, 132
$strokeWidth = 22
$dotRadius = 16

function New-RoundedRectangle([float]$size, [float]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($size - $d, 0, $d, $d, 270, 90)
    $path.AddArc($size - $d, $size - $d, $d, $d, 0, 90)
    $path.AddArc(0, $size - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Render-Icon([int]$size) {
    $scale = $size / 256.0
    $bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)

        $background = New-RoundedRectangle $size ($cornerRadius * $scale)
        $brush = New-Object System.Drawing.SolidBrush $accent
        $g.FillPath($brush, $background)

        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ($strokeWidth * $scale)
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $cx = $centerX * $scale
        $cy = $centerY * $scale
        foreach ($radius in $arcRadii) {
            $r = $radius * $scale
            $g.DrawArc($pen, $cx - $r, $cy - $r, 2 * $r, 2 * $r, 225, 90)
        }
        $dot = $dotRadius * $scale
        $g.FillEllipse([System.Drawing.Brushes]::White, $cx - $dot, $cy - $dot, 2 * $dot, 2 * $dot)
    }
    finally {
        $g.Dispose()
    }
    return $bitmap
}

$entries = foreach ($size in $sizes) {
    $bitmap = Render-Icon $size
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    if ($PreviewPng -and $size -eq 256) {
        $bitmap.Save($PreviewPng, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    $bitmap.Dispose()
    [pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() }
}

# ICO container: ICONDIR header, one ICONDIRENTRY per image, then the image data.
# (PowerShell variable names are case-insensitive: this must not be called $output, the [string] parameter.)
$container = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $container
$writer.Write([uint16]0)                # reserved
$writer.Write([uint16]1)                # type: icon
$writer.Write([uint16]$entries.Count)
$offset = 6 + 16 * $entries.Count
foreach ($entry in $entries) {
    $dimension = if ($entry.Size -ge 256) { 0 } else { $entry.Size }
    $writer.Write([byte]$dimension)     # width, 0 means 256
    $writer.Write([byte]$dimension)     # height
    $writer.Write([byte]0)              # palette size
    $writer.Write([byte]0)              # reserved
    $writer.Write([uint16]1)            # color planes
    $writer.Write([uint16]32)           # bits per pixel
    $writer.Write([uint32]$entry.Bytes.Length)
    $writer.Write([uint32]$offset)
    $offset += $entry.Bytes.Length
}
foreach ($entry in $entries) {
    $writer.Write($entry.Bytes)
}
$writer.Flush()

$resolved = [System.IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force (Split-Path $resolved -Parent) | Out-Null
[System.IO.File]::WriteAllBytes($resolved, $container.ToArray())
Write-Host "Wrote $resolved ($($container.Length) bytes, $($entries.Count) sizes)"
