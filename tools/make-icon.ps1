# Generates app.ico (multi-size, 32bpp + AND mask) with System.Drawing only.
param(
    [Parameter(Mandatory = $true)][string]$Output
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sizes = @(16, 24, 32, 48, 64, 128, 256)

function New-IconPixels([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $inset = [Math]::Max(0.5, $size * 0.03)
    $rect = [System.Drawing.RectangleF]::new($inset, $inset, ($size - 2 * $inset), ($size - 2 * $inset))
    $radius = [Math]::Max(2.0, $size * 0.24)
    $d = 2 * $radius
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc(($rect.Right - $d), $rect.Y, $d, $d, 270, 90)
    $path.AddArc(($rect.Right - $d), ($rect.Bottom - $d), $d, $d, 0, 90)
    $path.AddArc($rect.X, ($rect.Bottom - $d), $d, $d, 90, 90)
    $path.CloseFigure()

    $bg = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 16, 22, 31))
    $g.FillPath($bg, $path)

    $cx = $size / 2.0
    $cy = $size / 2.0
    $thick = [Math]::Max(1.2, $size * 0.105)
    $rad = $size * 0.305
    $ring = [System.Drawing.RectangleF]::new(($cx - $rad), ($cy - $rad), (2 * $rad), (2 * $rad))

    $trackPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 46, 58, 76), $thick)
    $trackPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $trackPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawArc($trackPen, $ring, 135, 270)

    $fillPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 46, 194, 106), $thick)
    $fillPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $fillPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawArc($fillPen, $ring, 135, 194)

    $dotBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 230, 237, 243))
    $dot = $size * 0.085
    $g.FillEllipse($dotBrush, ($cx - $dot / 2), ($cy - $dot / 2), $dot, $dot)

    $g.Dispose()
    $trackPen.Dispose()
    $fillPen.Dispose()
    $bg.Dispose()
    $dotBrush.Dispose()
    $path.Dispose()

    $lock = $bmp.LockBits([System.Drawing.Rectangle]::new(0, 0, $size, $size),
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bytes = New-Object byte[] ($lock.Stride * $size)
    [System.Runtime.InteropServices.Marshal]::Copy($lock.Scan0, $bytes, 0, $bytes.Length)
    $stride = $lock.Stride
    $bmp.UnlockBits($lock)
    $bmp.Dispose()

    return [pscustomobject]@{ Size = $size; Pixels = $bytes; Stride = $stride }
}

function Add-IconImage([int]$size, $target) {
    $rgba = New-IconPixels -size $size
    $maskRow = [int]([Math]::Floor(($size + 31) / 32) * 4)
    $xorSize = $size * $size * 4
    $andSize = $maskRow * $size

    $img = [System.IO.MemoryStream]::new()
    $w = [System.IO.BinaryWriter]::new($img)
    $w.Write([int]40)
    $w.Write([int]$size)
    $w.Write([int]($size * 2))
    $w.Write([int16]1)
    $w.Write([int16]32)
    $w.Write([int]0)
    $w.Write([int]($xorSize + $andSize))
    $w.Write([int]0)
    $w.Write([int]0)
    $w.Write([int]0)
    $w.Write([int]0)

    for ($y = $size - 1; $y -ge 0; $y--) {
        $w.Write($rgba.Pixels, ($y * $rgba.Stride), ($size * 4))
    }

    $zeros = New-Object byte[] $maskRow
    for ($y = 0; $y -lt $size; $y++) {
        $w.Write($zeros, 0, $maskRow)
    }
    $w.Flush()
    $target.Add($img.ToArray())
    $w.Dispose()
    $img.Dispose()
}

$blobs = [System.Collections.Generic.List[byte[]]]::new()
foreach ($s in $sizes) { Add-IconImage -size $s -target $blobs }

$out = [System.IO.MemoryStream]::new()
$writer = [System.IO.BinaryWriter]::new($out)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)

$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $blob = $blobs[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $writer.Write([byte]$dim)
    $writer.Write([byte]$dim)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$blob.Length)
    $writer.Write([uint32]$offset)
    $offset += $blob.Length
}

foreach ($blob in $blobs) { $writer.Write($blob, 0, $blob.Length) }
$writer.Flush()

$dir = Split-Path -Parent $Output
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
[System.IO.File]::WriteAllBytes($Output, $out.ToArray())
$writer.Dispose()
$out.Dispose()

Write-Host ("ikon dibuat: {0} ({1} bytes, {2} ukuran)" -f $Output, (Get-Item $Output).Length, $sizes.Count)
