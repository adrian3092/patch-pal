param(
    [string]$OutDir = 'assets\icon'
)
# Draws the Patch Pal app icon (band-aid on a Fluent-blue rounded square) with
# GDI+ at each standard icon size. Each size is drawn from vectors — no
# downscaling — so small sizes stay crisp; fine details drop out below 32 px.
Add-Type -AssemblyName System.Drawing

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Draw-Icon([System.Drawing.Graphics]$g, [int]$size) {
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $s = $size / 256.0   # all coordinates are designed on a 256-px canvas

    # Background: rounded square, vertical Fluent-blue gradient.
    $bgRect = New-RoundedRect (8 * $s) (8 * $s) (240 * $s) (240 * $s) (52 * $s)
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF(0, (8 * $s))),
        (New-Object System.Drawing.PointF(0, (248 * $s))),
        [System.Drawing.Color]::FromArgb(255, 43, 155, 232),
        [System.Drawing.Color]::FromArgb(255, 0, 91, 161))
    $g.FillPath($grad, $bgRect)
    $grad.Dispose(); $bgRect.Dispose()

    # Band-aid: white strip at 45 deg with a lighter centre pad.
    $state = $g.Save()
    $g.TranslateTransform((128 * $s), (128 * $s))
    $g.RotateTransform(-45)

    $stripW = 184 * $s; $stripH = 76 * $s; $stripR = 38 * $s
    $strip = New-RoundedRect (-$stripW / 2) (-$stripH / 2) $stripW $stripH $stripR
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 252, 253, 255))
    $g.FillPath($white, $strip)
    $white.Dispose(); $strip.Dispose()

    $padW = 78 * $s; $padH = 76 * $s; $padR = 10 * $s
    $pad = New-RoundedRect (-$padW / 2) (-$padH / 2) $padW $padH $padR
    $padBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 209, 230, 248))
    $g.FillPath($padBrush, $pad)
    $padBrush.Dispose(); $pad.Dispose()

    # Pad dots — skipped below 48 px, where they would just be noise.
    if ($size -ge 48) {
        $dot = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 124, 178, 224))
        $r = 7 * $s
        foreach ($cx in @(-17, 17)) {
            foreach ($cy in @(-17, 17)) {
                $g.FillEllipse($dot, [float](($cx * $s) - $r), [float](($cy * $s) - $r), [float](2 * $r), [float](2 * $r))
            }
        }
        $dot.Dispose()
    }
    $g.Restore($state)
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
foreach ($size in 16, 24, 32, 48, 64, 128, 256) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    Draw-Icon $g $size
    $g.Dispose()
    $path = Join-Path $OutDir "icon-$size.png"
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "wrote $path"
}
