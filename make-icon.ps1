# Duru 아이콘(D + 글줄): 1024px 원본을 그린 뒤 크기별로 고품질 축소해서 .ico 로 저장
# 작은 크기(<=64)는 호환성 좋은 32비트 BMP, 큰 크기는 PNG 로 넣는다.
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot 'app.ico'
$S = 1024

function Draw-Master {
  $bmp = New-Object System.Drawing.Bitmap $S, $S
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'
  $g.Clear([System.Drawing.Color]::Transparent)
  $k = $S / 256.0
  $m = 6 * $k; $w = $S - 2 * $m; $r = 56 * $k
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $path.AddArc($m, $m, $r, $r, 180, 90); $path.AddArc($m + $w - $r, $m, $r, $r, 270, 90)
  $path.AddArc($m + $w - $r, $m + $w - $r, $r, $r, 0, 90); $path.AddArc($m, $m + $w - $r, $r, $r, 90, 90); $path.CloseFigure()
  $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, 0)), ([System.Drawing.PointF]::new($S, $S)), ([System.Drawing.Color]::FromArgb(46, 58, 80)), ([System.Drawing.Color]::FromArgb(17, 24, 39))
  $g.FillPath($bg, $path)
  # D (펼친 두루마리 모양) + 안쪽 글줄
  $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), (24 * $k)
  $pen.LineJoin = 'Round'; $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
  $d = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d.AddLine(72*$k, 192*$k, 72*$k, 64*$k)
  $d.AddLine(72*$k, 64*$k, 124*$k, 64*$k)
  $d.AddArc(60*$k, 64*$k, 128*$k, 128*$k, 270, 180)
  $d.AddLine(124*$k, 192*$k, 72*$k, 192*$k)
  $d.CloseFigure()
  $g.DrawPath($pen, $d)
  $blue = [System.Drawing.Color]::FromArgb(56, 189, 248)
  $pen2 = New-Object System.Drawing.Pen $blue, (16 * $k); $pen2.StartCap = 'Round'; $pen2.EndCap = 'Round'
  $g.DrawLine($pen2, 104*$k, 110*$k, 150*$k, 110*$k)
  $g.DrawLine($pen2, 104*$k, 146*$k, 136*$k, 146*$k)
  $g.Dispose()
  return $bmp
}

function Resize($src, $n) {
  $b = New-Object System.Drawing.Bitmap $n, $n
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.InterpolationMode = 'HighQualityBicubic'; $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'; $g.CompositingQuality = 'HighQuality'
  $attr = New-Object System.Drawing.Imaging.ImageAttributes; $attr.SetWrapMode('TileFlipXY')
  $g.DrawImage($src, (New-Object System.Drawing.Rectangle 0, 0, $n, $n), 0, 0, $src.Width, $src.Height, 'Pixel', $attr)
  $g.Dispose(); return $b
}

function To-Dib($b) {
  $n = $b.Width; $ms = New-Object IO.MemoryStream; $w = New-Object IO.BinaryWriter $ms
  $w.Write([UInt32]40); $w.Write([Int32]$n); $w.Write([Int32]($n * 2)); $w.Write([UInt16]1); $w.Write([UInt16]32)
  $w.Write([UInt32]0); $w.Write([UInt32]0); $w.Write([Int32]0); $w.Write([Int32]0); $w.Write([UInt32]0); $w.Write([UInt32]0)
  for ($y = $n - 1; $y -ge 0; $y--) { for ($x = 0; $x -lt $n; $x++) { $c = $b.GetPixel($x, $y); $w.Write([byte]$c.B); $w.Write([byte]$c.G); $w.Write([byte]$c.R); $w.Write([byte]$c.A) } }
  $rowBytes = [int]([Math]::Ceiling($n / 32.0) * 4)
  $w.Write((New-Object byte[] ($rowBytes * $n)))   # AND 마스크 (알파 채널 사용하므로 0)
  $w.Flush(); return ,$ms.ToArray()
}

$master = Draw-Master
$sizes = 16, 20, 24, 32, 40, 48, 64, 96, 128, 256
$data = @()
foreach ($n in $sizes) {
  $b = Resize $master $n
  if ($n -le 64) { $data += , (To-Dib $b) }
  else { $ms = New-Object IO.MemoryStream; $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $data += , $ms.ToArray() }
  if ($n -eq 256) { $b.Save((Join-Path $PSScriptRoot 'icon.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
  if ($n -eq 32) { $b.Save((Join-Path $PSScriptRoot 'icon-32.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
  $b.Dispose()
}
$fs = [IO.File]::Create($out); $bw = New-Object IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $n = $sizes[$i]; $dim = if ($n -ge 256) { 0 } else { $n }
  $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
  $bw.Write([UInt16]1); $bw.Write([UInt16]32); $bw.Write([UInt32]([byte[]]$data[$i]).Length); $bw.Write([UInt32]$offset)
  $offset += $data[$i].Length
}
foreach ($d in $data) { $bw.Write([byte[]]$d) }
$bw.Close()
