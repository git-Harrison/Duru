# Duru 아이콘(D + 글줄) → app.ico, icon.png(256), icon-32.png
# 선명하게 만드는 방법:
#  - 크기마다 따로 그린다 (큰 그림 하나를 줄이지 않음). 목표 크기의 4배로 그린 뒤 한 번만 줄여 가장자리가 깔끔함
#  - 작은 크기(24px 이하)는 선을 조금 굵게 해서 뭉개지지 않게
#  - Windows 가 화면 배율(100~300%)마다 고르는 크기를 전부 넣음
#  - 64px 이하는 모든 Windows 구성 요소가 읽는 32비트 BMP, 그 위는 PNG
Add-Type -AssemblyName System.Drawing
$d = $PSScriptRoot

function Draw-Icon($g, [double]$k, [int]$size) {
  $bold = if ($size -le 16) { 1.35 } elseif ($size -le 24) { 1.2 } elseif ($size -le 32) { 1.08 } else { 1.0 }
  $m = 6 * $k; $w = 256 * $k - 2 * $m; $r = 56 * $k
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $path.AddArc($m, $m, $r, $r, 180, 90); $path.AddArc($m + $w - $r, $m, $r, $r, 270, 90)
  $path.AddArc($m + $w - $r, $m + $w - $r, $r, $r, 0, 90); $path.AddArc($m, $m + $w - $r, $r, $r, 90, 90); $path.CloseFigure()
  $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, 0)), ([System.Drawing.PointF]::new(256 * $k, 256 * $k)), ([System.Drawing.Color]::FromArgb(46, 58, 80)), ([System.Drawing.Color]::FromArgb(17, 24, 39))
  $g.FillPath($bg, $path)
  $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), (24 * $k * $bold)
  $pen.LineJoin = 'Round'; $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
  $dp = New-Object System.Drawing.Drawing2D.GraphicsPath
  $dp.AddLine(72 * $k, 192 * $k, 72 * $k, 64 * $k)
  $dp.AddLine(72 * $k, 64 * $k, 124 * $k, 64 * $k)
  $dp.AddArc(60 * $k, 64 * $k, 128 * $k, 128 * $k, 270, 180)
  $dp.AddLine(124 * $k, 192 * $k, 72 * $k, 192 * $k)
  $dp.CloseFigure()
  $g.DrawPath($pen, $dp)
  $pen2 = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(56, 189, 248)), (16 * $k * $bold); $pen2.StartCap = 'Round'; $pen2.EndCap = 'Round'
  $g.DrawLine($pen2, 104 * $k, 110 * $k, 150 * $k, 110 * $k)
  $g.DrawLine($pen2, 104 * $k, 146 * $k, 136 * $k, 146 * $k)
}

. (Join-Path $d 'icon-lib.ps1')
Build-Ico ${function:Draw-Icon} (Join-Path $d 'app.ico') (Join-Path $d 'icon.png') (Join-Path $d 'icon-32.png')
