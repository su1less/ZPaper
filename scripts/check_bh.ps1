Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap('C:\Users\ASUS\AppData\Local\TechRainWallpaper\preview_bh2.png')
$W = $bmp.Width; $H = $bmp.Height
$bhX = [int]($W * 0.44); $bhY = [int]($H * 0.44)
$bhR = [int]($H * 0.115)
function RingAvg($bm, $cx, $cy, $r) {
    $sum = 0.0; $n = 0
    for ($a = 0; $a -lt 360; $a += 6) {
        $x = [int]($cx + $r * [Math]::Cos($a * [Math]::PI / 180))
        $y = [int]($cy + $r * [Math]::Sin($a * [Math]::PI / 180))
        if ($x -ge 0 -and $x -lt $bm.Width -and $y -ge 0 -and $y -lt $bm.Height) {
            $c = $bm.GetPixel($x, $y); $sum += ($c.R + $c.G + $c.B) / 3.0; $n++
        }
    }
    if ($n -eq 0) { return -1 } else { return [math]::Round($sum / $n, 1)
    }
}
$inside = RingAvg $bmp $bhX $bhY ($bhR * 0.4)
$ring   = RingAvg $bmp $bhX $bhY ($bhR * 1.08)
$diskOut= RingAvg $bmp $bhX $bhY ($bhR * 1.9)
$rand = New-Object System.Random(5)
$sum = 0.0
for ($i = 0; $i -lt 2000; $i++) { $c = $bmp.GetPixel($rand.Next($W), $rand.Next($H)); $sum += ($c.R+$c.G+$c.B)/3.0 }
"inside horizon avg = $inside (expect very dark)"
"photon ring avg    = $ring (expect much brighter than inside)"
"outer disk avg     = $diskOut"
"whole image avg    = {0:N1}" -f ($sum/2000)
$bmp.Dispose()
if ($inside -lt 40 -and $ring -gt ($inside + 40)) { "VERDICT: black hole structure OK (ring {0} vs inside {1})" -f $ring, $inside }
else { "VERDICT: structure unclear, check manually" }
