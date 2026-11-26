param([string]$ProjectRoot = "D:\UnityProject\CafeTowerDefense")
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"

function C([string]$hex, [int]$a = 255) {
    $c = [System.Drawing.ColorTranslator]::FromHtml($hex)
    return [System.Drawing.Color]::FromArgb($a, $c.R, $c.G, $c.B)
}
function P([float]$x, [float]$y) { New-Object System.Drawing.PointF $x, $y }
function RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); return $p
}

$ink = C '#2A170E'
$outline = New-Object System.Drawing.Pen $ink, 14
$outline.LineJoin = 'Round'
$thin = New-Object System.Drawing.Pen $ink, 9
$thin.StartCap = 'Round'; $thin.EndCap = 'Round'; $thin.LineJoin = 'Round'

function Draw-Background([System.Drawing.Graphics]$g, [float]$w, [float]$h, [float]$cx, [float]$cy) {
    $g.Clear((C '#2E1C12'))
    $ell = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = [Math]::Max($w, $h) * 0.95
    $ell.AddEllipse($cx - $r, $cy - $r, $r * 2, $r * 2)
    $br = New-Object System.Drawing.Drawing2D.PathGradientBrush $ell
    $br.CenterColor = C '#A86E47'
    $br.SurroundColors = [System.Drawing.Color[]]@((C '#2E1C12'))
    $br.CenterPoint = P $cx $cy
    $g.FillPath($br, $ell)
}

# 1024 기준 좌표계에서 그림 (호출 전에 ScaleTransform/TranslateTransform 으로 크기·위치 조절)
function Draw-Art([System.Drawing.Graphics]$g, [bool]$floor = $true) {
    # 바닥 체크 타일 (아래쪽에 은은하게)
    if ($floor) { for ($i = 0; $i -lt 8; $i++) {
        $b = New-Object System.Drawing.SolidBrush (C '#FFFFFF' ($(if ($i % 2 -eq 0) { 18 } else { 8 })))
        $g.FillRectangle($b, $i * 128, 880, 128, 144)
    } }

    # 컵 그림자
    $g.FillEllipse((New-Object System.Drawing.SolidBrush (C '#000000' 90)), 318, 828, 388, 72)

    # 빨대 (뚜껑 뒤)
    $strawW = New-Object System.Drawing.Pen (C '#FFF3E2'), 40
    $strawW.StartCap = 'Round'; $strawW.EndCap = 'Round'
    $strawO = New-Object System.Drawing.Pen $ink, 56
    $strawO.StartCap = 'Round'; $strawO.EndCap = 'Round'
    $g.DrawLine($strawO, 590, 330, 676, 130)
    $g.DrawLine($strawW, 590, 330, 676, 130)
    $stripe = New-Object System.Drawing.Pen (C '#FF6F8E'), 40
    foreach ($t in 0.15, 0.45, 0.75) {
        $x1 = 590 + (676 - 590) * $t; $y1 = 330 + (130 - 330) * $t
        $x2 = 590 + (676 - 590) * ($t + 0.12); $y2 = 330 + (130 - 330) * ($t + 0.12)
        $g.DrawLine($stripe, $x1, $y1, $x2, $y2)
    }

    # 컵 몸통 (딸기 음료)
    $body = [System.Drawing.PointF[]]@((P 358 400), (P 666 400), (P 628 862), (P 396 862))
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush (P 0 400), (P 0 862), (C '#FF9BB0'), (C '#E04866')
    $g.FillPolygon($grad, $body)
    # 하이라이트
    $hl = [System.Drawing.PointF[]]@((P 392 430), (P 432 430), (P 438 820), (P 420 820))
    $g.FillPolygon((New-Object System.Drawing.SolidBrush (C '#FFFFFF' 70)), $hl)
    $g.DrawPolygon($outline, $body)

    # 종이 슬리브
    $sleeve = [System.Drawing.PointF[]]@((P 371 562), (P 653 562), (P 642 692), (P 382 692))
    $g.FillPolygon((New-Object System.Drawing.SolidBrush (C '#C4935F')), $sleeve)
    $g.DrawPolygon($outline, $sleeve)

    # 커피콩 무늬
    $state = $g.Save()
    $g.TranslateTransform(512, 627); $g.RotateTransform(25)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush (C '#5A3420')), -34, -46, 68, 92)
    $bean = New-Object System.Drawing.Pen (C '#F3D9B5'), 7
    $bean.StartCap = 'Round'; $bean.EndCap = 'Round'
    $g.DrawBezier($bean, 0, -36, -14, -10, 14, 10, 0, 36)
    $g.Restore($state)

    # 성벽 모양 뚜껑 (타워)
    $cream = New-Object System.Drawing.SolidBrush (C '#FFF3E2')
    foreach ($mx in 336, 466, 596) {
        $m = RoundRect $mx 232 92 110 14
        $g.FillPath($cream, $m); $g.DrawPath($outline, $m)
    }
    $lid = RoundRect 330 318 364 96 18
    $g.FillPath($cream, $lid); $g.DrawPath($outline, $lid)
    # 화살창
    $slit = RoundRect 500 340 24 52 10
    $g.FillPath((New-Object System.Drawing.SolidBrush $ink), $slit)
    # 깃발
    $g.DrawLine($thin, 512, 232, 512, 150)
    $flag = [System.Drawing.PointF[]]@((P 512 152), (P 578 172), (P 512 194))
    $g.FillPolygon((New-Object System.Drawing.SolidBrush (C '#7CC46A')), $flag)
    $g.DrawPolygon($thin, $flag)

    # 개미 (왼쪽 아래, 컵 쪽으로 다가옴)
    $antB = New-Object System.Drawing.SolidBrush (C '#2A1810')
    foreach ($leg in @(@(160, 830, 135, 880), @(205, 832, 200, 885), @(240, 828, 262, 880), @(165, 815, 128, 778), @(210, 812, 206, 768), @(242, 812, 270, 770))) {
        $lp = New-Object System.Drawing.Pen (C '#2A1810'), 8
        $lp.StartCap = 'Round'; $lp.EndCap = 'Round'
        $g.DrawLine($lp, $leg[0], $leg[1], $leg[2], $leg[3])
    }
    $g.FillEllipse($antB, 110, 788, 96, 74)
    $g.FillEllipse($antB, 196, 800, 54, 46)
    $g.FillEllipse($antB, 240, 784, 70, 64)
    $ap = New-Object System.Drawing.Pen (C '#2A1810'), 7
    $ap.StartCap = 'Round'; $ap.EndCap = 'Round'
    $g.DrawBezier($ap, 285, 790, 295, 760, 315, 750, 330, 748)
    $g.DrawBezier($ap, 272, 788, 270, 755, 285, 742, 300, 735)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush (C '#FFFFFF')), 278, 800, 20, 20)
    $g.FillEllipse($antB, 286, 806, 10, 10)

    # 컵에서 개미에게 날아가는 딸기 방울
    $drop = New-Object System.Drawing.SolidBrush (C '#FF6F8E')
    $g.FillEllipse($drop, 318, 712, 38, 38); $g.DrawEllipse($thin, 318, 712, 38, 38)
    $g.FillEllipse($drop, 344, 652, 28, 28); $g.DrawEllipse($thin, 344, 652, 28, 28)
    $speed = New-Object System.Drawing.Pen (C '#FFF3E2' 160), 7
    $speed.StartCap = 'Round'; $speed.EndCap = 'Round'
    $g.DrawLine($speed, 370, 718, 410, 708); $g.DrawLine($speed, 366, 740, 400, 738)

    # 반짝이
    foreach ($s in @(@(770, 300, 34), @(250, 330, 24), @(800, 520, 20))) {
        $x = $s[0]; $y = $s[1]; $r = $s[2]
        $star = [System.Drawing.PointF[]]@((P $x ($y - $r)), (P ($x + $r * 0.28) ($y - $r * 0.28)), (P ($x + $r) $y), (P ($x + $r * 0.28) ($y + $r * 0.28)),
            (P $x ($y + $r)), (P ($x - $r * 0.28) ($y + $r * 0.28)), (P ($x - $r) $y), (P ($x - $r * 0.28) ($y - $r * 0.28)))
        $g.FillPolygon((New-Object System.Drawing.SolidBrush (C '#FFE8A8')), $star)
    }
}

function New-Canvas([int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    return @($bmp, $g)
}

# ---- 앱 아이콘 1024 (Unity용) ----
$bmp, $g = New-Canvas 1024 1024
Draw-Background $g 1024 1024 512 470
Draw-Art $g
$iconDir = Join-Path $ProjectRoot "Assets\CafeTower\Icons"
New-Item -ItemType Directory -Force $iconDir | Out-Null
$icon1024 = Join-Path $iconDir "AppIcon.png"
$bmp.Save($icon1024, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()


# ---- Android 적응형 아이콘 레이어 1024 ----
# 기기마다 원·둥근 사각형 등으로 잘리므로 그림 전체를 가운데 안전 영역(지름 61%) 안에 넣음
$adBg, $gb = New-Canvas 1024 1024
Draw-Background $gb 1024 1024 512 470
$adBg.Save((Join-Path $iconDir "AppIconBackground.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$gb.Dispose(); $adBg.Dispose()

$adFg, $gf = New-Canvas 1024 1024
$gf.Clear([System.Drawing.Color]::Transparent)
$gf.TranslateTransform(512, 512); $gf.ScaleTransform(0.64, 0.64); $gf.TranslateTransform(-420, -490)
Draw-Art $gf $false
$adFg.Save((Join-Path $iconDir "AppIconForeground.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$gf.Dispose(); $adFg.Dispose()
# ---- 스토어 아이콘 512 ----
$storeDir = Join-Path $ProjectRoot "StoreAssets"
New-Item -ItemType Directory -Force $storeDir | Out-Null
$small, $g2 = New-Canvas 512 512
$g2.DrawImage($bmp, 0, 0, 512, 512)
$small.Save((Join-Path $storeDir "icon-512.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$g2.Dispose(); $small.Dispose(); $bmp.Dispose()

# ---- 그래픽 이미지 1024x500 ----
$fg, $g3 = New-Canvas 1024 500
Draw-Background $g3 1024 500 300 230
for ($i = 0; $i -lt 18; $i++) { $g3.FillRectangle((New-Object System.Drawing.SolidBrush (C '#FFFFFF' ($(if ($i % 2 -eq 0) { 18 } else { 8 })))), $i * 59, 425, 59, 75) }
$st = $g3.Save()
$g3.TranslateTransform(30, 20); $g3.ScaleTransform(0.46, 0.46)
Draw-Art $g3 $false
$g3.Restore($st)
$titleFont = New-Object System.Drawing.Font "Malgun Gothic", 60, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
$subFont = New-Object System.Drawing.Font "Malgun Gothic", 30, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
$shadow = New-Object System.Drawing.SolidBrush (C '#000000' 110)
$g3.DrawString("카페 타워 디펜스", $titleFont, $shadow, 494, 166)
$g3.DrawString("카페 타워 디펜스", $titleFont, (New-Object System.Drawing.SolidBrush (C '#FFF3E2')), 490, 160)
$g3.DrawString("벌레로부터 디저트를 지켜라!", $subFont, (New-Object System.Drawing.SolidBrush (C '#F5BE6B')), 494, 258)
$fg.Save((Join-Path $storeDir "feature-graphic-1024x500.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$g3.Dispose(); $fg.Dispose()

Get-ChildItem $iconDir, $storeDir -Filter *.png | ForEach-Object { "{0}  {1:N0} bytes" -f $_.FullName, $_.Length }
