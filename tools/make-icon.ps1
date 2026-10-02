# uygulama ikonunu (GorevTakip/Assets/app.ico) üretir.
# AppLogo.xaml'daki yuvarlak kare + tik çiziminin aynısı, ikon değişecekse ikisini birlikte güncelle
param([string]$Out = (Join-Path $PSScriptRoot '..\GorevTakip\Assets\app.ico'))

Add-Type -AssemblyName System.Drawing

function New-IconPng([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    # 100x100 koordinatta çiziyoruz, xaml ile aynı sayılar kullanılsın diye
    $g.ScaleTransform($s / 100.0, $s / 100.0)

    $cyan = [System.Drawing.Color]::FromArgb(255, 0, 200, 252)
    $blue = [System.Drawing.Color]::FromArgb(255, 43, 123, 255)
    $bounds = [System.Drawing.RectangleF]::new(6, 6, 88, 88)

    # sol üstten sağ alta renk geçişli, yuvarlak köşeli kare
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $x = 6; $y = 6; $w = 88; $h = 88; $d = 48
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $bounds, $cyan, $blue, ([single]45)
    $g.FillPath($brush, $path)

    # tik. 32px altında ince kalıyor, küçük boyutta biraz kalınlaştırıyoruz
    $tickWidth = if ($s -lt 32) { 13 } else { 10 }
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([single]$tickWidth)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawLines($pen, [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new(29, 52),
            [System.Drawing.PointF]::new(44, 67),
            [System.Drawing.PointF]::new(72, 36)))
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 256
$pngs = foreach ($s in $sizes) { , (New-IconPng $s) }

# ico dosyası: 6 byte başlık + her boyut için 16 byte kayıt + png verileri
$ico = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $ico
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $b = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }   # 256 boyutu 0 olarak yazılıyor
    $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$pngs[$i].Length); $w.Write([UInt32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write([byte[]]$p) }
$w.Flush()

$Out = [System.IO.Path]::GetFullPath($Out)
[System.IO.File]::WriteAllBytes($Out, $ico.ToArray())
Write-Output "ikon yazildi: $Out"
