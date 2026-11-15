# Generates the MSIX visual assets AND the app .ico from one source: a dark terminal window with a
# green prompt and a green "running" dot, on a light rounded tile.
# Run with Windows PowerShell: powershell.exe -NoProfile -File generate-msix-images.ps1
# ASCII only (Windows PowerShell 5.1 reads BOM-less .ps1 as ANSI).
Add-Type -AssemblyName System.Drawing

$repoRoot  = Split-Path $PSScriptRoot -Parent
$imagesDir = Join-Path $PSScriptRoot "Images"
$icoPath   = Join-Path $repoRoot "WslMonitor\Resources\WslMonitor.ico"
New-Item -ItemType Directory -Force $imagesDir | Out-Null
New-Item -ItemType Directory -Force (Split-Path $icoPath -Parent) | Out-Null

$tile   = [System.Drawing.Color]::FromArgb(255, 233, 240, 250)  # E9F0FA  light tile
$frame  = [System.Drawing.Color]::FromArgb(255, 45, 50, 59)     # 2D323B  window frame
$screen = [System.Drawing.Color]::FromArgb(255, 24, 28, 36)     # 181C24  terminal background
$prompt = [System.Drawing.Color]::FromArgb(255, 34, 197, 94)    # 22C55E  prompt + status dot
$white  = [System.Drawing.Color]::White

function Rounded($x, $y, $w, $h, $r) {
    $d = [single]($r * 2)
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc([single]$x, [single]$y, $d, $d, 180, 90)
    $p.AddArc([single]($x + $w - $d), [single]$y, $d, $d, 270, 90)
    $p.AddArc([single]($x + $w - $d), [single]($y + $h - $d), $d, $d, 0, 90)
    $p.AddArc([single]$x, [single]($y + $h - $d), $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Fill($g, $color, $path) {
    $b = New-Object System.Drawing.SolidBrush($color)
    $g.FillPath($b, $path); $b.Dispose(); $path.Dispose()
}

# Terminal window + status dot centered in a square of side S at (ox, oy).
function Draw-Mark($g, $S, $ox, $oy) {
    $ww = [single]($S * 0.70); $wh = [single]($S * 0.56)
    $wx = [single]($ox + ($S - $ww) / 2); $wy = [single]($oy + $S * 0.20)
    Fill $g $frame (Rounded $wx $wy $ww $wh ([single]($S * 0.07)))
    # title bar strip stays frame-coloured; the screen sits below it
    $bz = [single]($S * 0.035); $tb = [single]($S * 0.09)
    Fill $g $screen (Rounded ($wx + $bz) ($wy + $tb) ($ww - 2 * $bz) ($wh - $tb - $bz) ([single]($S * 0.035)))
    # prompt: chevron and cursor
    $pen = New-Object System.Drawing.Pen($prompt, [single][Math]::Max(1.0, $S * 0.055))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round; $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $cx = [single]($wx + $ww * 0.18); $cy = [single]($wy + $tb + ($wh - $tb) * 0.45); $ch = [single]($S * 0.09)
    $g.DrawLines($pen, @(
        (New-Object System.Drawing.PointF($cx, [single]($cy - $ch))),
        (New-Object System.Drawing.PointF([single]($cx + $ch), $cy)),
        (New-Object System.Drawing.PointF($cx, [single]($cy + $ch)))))
    $g.DrawLine($pen, [single]($cx + $ch * 1.6), [single]($cy + $ch), [single]($cx + $ch * 3.2), [single]($cy + $ch))
    $pen.Dispose()
    # status dot (bottom-right), ringed in the tile colour
    $r = [single]($S * 0.13)
    $dx = [single]($wx + $ww - $r * 0.4); $dy = [single]($wy + $wh - $r * 0.4)
    $ring = New-Object System.Drawing.Drawing2D.GraphicsPath
    $ring.AddEllipse([single]($dx - $r - $S * 0.025), [single]($dy - $r - $S * 0.025), [single](2 * $r + $S * 0.05), [single](2 * $r + $S * 0.05))
    Fill $g $tile $ring
    $dot = New-Object System.Drawing.Drawing2D.GraphicsPath
    $dot.AddEllipse([single]($dx - $r), [single]($dy - $r), [single](2 * $r), [single](2 * $r))
    Fill $g $prompt $dot
}

function New-Master([int]$M) {
    $bmp = New-Object System.Drawing.Bitmap($M, $M, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)
    Fill $g $tile (Rounded 0 0 $M $M ([single]($M * 0.18)))
    Draw-Mark $g $M 0 0
    $g.Dispose()
    return $bmp
}

function New-Resized($src, [int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($src, 0, 0, $w, $h)
    $g.Dispose()
    return $bmp
}

# Render each asset at its own size with 4x supersampling (render large, shrink once).
$SS = 4
function New-Crisp([int]$size) {
    $m = New-Master ($size * $SS)
    $r = New-Resized $m $size $size
    $m.Dispose()
    return $r
}

function Save-Square([int]$size, [string]$name) {
    $b = New-Crisp $size
    $b.Save((Join-Path $imagesDir $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $b.Dispose()
    Write-Output "  $name ($size x $size)"
}

function Save-Splash([int]$w, [int]$h, [string]$name) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = [int]($h * 0.5); $bx = [int](($w - $s) / 2); $by = [int](($h - $s) / 2)
    $bb = New-Crisp $s
    $g.DrawImage($bb, $bx, $by, $s, $s)
    $bb.Dispose(); $g.Dispose()
    $bmp.Save((Join-Path $imagesDir $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "  $name ($w x $h)"
}

function Save-Ico([int[]]$sizes, [string]$path) {
    $blobs = @()
    foreach ($s in $sizes) {
        $b = New-Crisp $s
        $ms = New-Object System.IO.MemoryStream
        $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $blobs += , ($ms.ToArray())
        $ms.Dispose(); $b.Dispose()
    }
    $fs = [System.IO.File]::Create($path)
    $bw = New-Object System.IO.BinaryWriter($fs)
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]; $len = $blobs[$i].Length
        $wb = if ($s -ge 256) { 0 } else { $s }
        $bw.Write([byte]$wb); $bw.Write([byte]$wb); $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]$len); $bw.Write([uint32]$offset)
        $offset += $len
    }
    foreach ($b in $blobs) { $bw.Write($b) }
    $bw.Flush(); $bw.Close(); $fs.Close()
    Write-Output "  WslMonitor.ico ($($sizes -join ', '))"
}

Write-Output "Generating MSIX images:"
# Every scale and target size Windows asks for, so the taskbar / Start / Settings never upscale a
# smaller asset (which is what makes packaged app icons look blurry at 125-300% scaling).
Remove-Item (Join-Path $imagesDir "*.png") -ErrorAction SilentlyContinue
foreach ($s in @(@(100, 1.0), @(125, 1.25), @(150, 1.5), @(200, 2.0), @(400, 4.0))) {
    $scale = $s[0]; $f = $s[1]
    $sfx = if ($scale -eq 100) { "" } else { ".scale-$scale" }
    Save-Square ([int](50 * $f))  "StoreLogo$sfx.png"
    Save-Square ([int](44 * $f))  "Square44x44Logo$sfx.png"
    Save-Square ([int](150 * $f)) "Square150x150Logo$sfx.png"
    Save-Splash ([int](620 * $f)) ([int](300 * $f)) "SplashScreen$sfx.png"
}
# Square44x44Logo is the taskbar / title bar / Start list icon: provide exact pixel sizes.
foreach ($t in @(16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256)) {
    Save-Square $t "Square44x44Logo.targetsize-$t.png"
    Save-Square $t "Square44x44Logo.targetsize-${t}_altform-unplated.png"
    Save-Square $t "Square44x44Logo.targetsize-${t}_altform-lightunplated.png"
}

Write-Output "Generating app icon:"
Save-Ico @(16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 128, 256) $icoPath
$png = New-Crisp 256
$png.Save((Join-Path (Split-Path $icoPath -Parent) "WslMonitor.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$png.Dispose()
Write-Output "  WslMonitor.png (256 x 256)"

Write-Output "Done."
