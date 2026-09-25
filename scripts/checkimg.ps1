Add-Type -AssemblyName System.Drawing
$path = 'C:\Users\ASUS\ZCodeProject\desktop_beautify\screen_check.png'
$bmp = New-Object System.Drawing.Bitmap($path)
$w = $bmp.Width; $h = $bmp.Height
$sum = 0.0; $n = 0
$rand = New-Object System.Random(7)
for ($i = 0; $i -lt 4000; $i++) {
    $x = $rand.Next($w); $y = $rand.Next($h)
    $c = $bmp.GetPixel($x, $y)
    $sum += ($c.R + $c.G + $c.B) / 3.0
    $n++
}
$avg = $sum / $n
"sampled $n pixels, average brightness = {0:N1} / 255" -f $avg
if ($avg -lt 110) { "VERDICT: dark scene rendered correctly (not white)" }
elseif ($avg -lt 160) { "VERDICT: medium brightness - wallpaper visible" }
else { "VERDICT: BRIGHT/WHITE - wallpaper may not be rendering!" }
$bmp.Dispose()
