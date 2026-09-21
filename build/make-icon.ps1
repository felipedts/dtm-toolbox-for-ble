<#
.SYNOPSIS
Renders the application icon and packs it into src\DtmToolbox\Resources\app.ico.

.DESCRIPTION
The icon (five bars of a channel chart on a rounded square) is drawn with GDI+ at every size the shell uses (16 to 256 px) and stored as
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

# Geometry on a 256 px canvas: rounded square background and five bars on a common baseline,
# the channel chart of the application reduced to a glyph.
$cornerRadius = 56
$accent = [System.Drawing.Color]::FromArgb(255, 0x00, 0xAC, 0xC1)
$barWidth = 26
$barGap = 14
$barHeights = 64, 118, 160, 96, 44
$baseline = 204
$barRadius = 6

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

function New-RoundedBar([float]$x, [float]$y, [float]$width, [float]$height, [float]$radius) {
    $r = [Math]::Min($radius, [Math]::Min($width, $height) / 2)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    if ($r -lt 0.5) {
        $path.AddRectangle((New-Object System.Drawing.RectangleF $x, $y, $width, $height))
        return $path
    }
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $width - $d, $y, $d, $d, 270, 90)
    $path.AddLine($x + $width, $y + $height, $x, $y + $height)
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

        $total = ($barWidth * $barHeights.Count) + ($barGap * ($barHeights.Count - 1))
        $x = (256 - $total) / 2
        foreach ($height in $barHeights) {
            $bar = New-RoundedBar ($x * $scale) (($baseline - $height) * $scale) ($barWidth * $scale) ($height * $scale) ($barRadius * $scale)
            $g.FillPath([System.Drawing.Brushes]::White, $bar)
            $x += $barWidth + $barGap
        }
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
