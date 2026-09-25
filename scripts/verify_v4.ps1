Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
# minimize windows to reveal desktop, screenshot, analyze regions
$sh = New-Object -ComObject Shell.Application
$sh.MinimizeAll()
Start-Sleep -Seconds 2
$b = [System.Windows.Forms.SystemInformation]::VirtualScreen
$bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.X, $b.Y, 0, 0, $bmp.Size)
$out = 'C:\Users\ASUS\ZCodeProject\desktop_beautify\screen_v4.png'
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)

function RegionStats($bm, $x, $y, $w, $h) {
    $rand = New-Object System.Random(11)
    $sum = 0.0; $mag = 0; $n = 600
    for ($i = 0; $i -lt $n; $i++) {
        $px = $x + $rand.Next($w); $py = $y + $rand.Next($h)
        $c = $bm.GetPixel($px, $py)
        $sum += ($c.R + $c.G + $c.B) / 3.0
        if ($c.R -gt 190 -and $c.B -gt 190 -and $c.G -lt 70) { $mag++ }
    }
    @{ avg = [math]::Round($sum / $n, 1); magenta = $mag }
}
$W = $bmp.Width; $H = $bmp.Height
$weather = RegionStats $bmp ($W-330) 20 300 190      # top-right weather panel
$dock    = RegionStats $bmp ([int]($W*0.38)) ($H-80) ([int]($W*0.24)) 70   # bottom-center dock
$vu      = RegionStats $bmp 20 ($H-90) 240 70        # bottom-left VU bars
"weather region: avg=$($weather.avg) magenta=$($weather.magenta)"
"dock region:    avg=$($dock.avg) magenta=$($dock.magenta)"
"vu region:      avg=$($vu.avg) magenta=$($vu.magenta)"
$g.Dispose(); $bmp.Dispose()
"saved $out"
