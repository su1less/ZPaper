$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class MInput {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
}
"@
function Shot($path) {
    $b = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($b.X, $b.Y, 0, 0, $bmp.Size)
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
}
Add-Type -AssemblyName System.Windows.Forms
# dock hides when a fullscreen window is foreground: clear the deck first
$sh = New-Object -ComObject Shell.Application
$sh.MinimizeAll()
Start-Sleep -Seconds 2
# find dock: bottom center band. sweep mouse across it slowly
$y = 1042   # inside dock panel (panel top ~ 1080-8-58=1014, so 1042 is inside)
# 1) rest screenshot (mouse away)
[MInput]::SetCursorPos(960, 200) | Out-Null
Start-Sleep -Milliseconds 900
Shot 'C:\Users\ASUS\ZCodeProject\desktop_beautify\dock_rest.png'
# 2) sweep across the dock
$cpu0 = (Get-Process TechRainWallpaper).TotalProcessorTime.TotalSeconds
$t0 = Get-Date
for ($x = 620; $x -le 1300; $x += 30) { [MInput]::SetCursorPos($x, $y) | Out-Null; Start-Sleep -Milliseconds 40 }
# 3) park on an icon, let the spring settle, screenshot
[MInput]::SetCursorPos(850, $y) | Out-Null
Start-Sleep -Milliseconds 900
$cpu1 = (Get-Process TechRainWallpaper).TotalProcessorTime.TotalSeconds
$el = ((Get-Date) - $t0).TotalSeconds
Shot 'C:\Users\ASUS\ZCodeProject\desktop_beautify\dock_hover.png'
[MInput]::SetCursorPos(960, 300) | Out-Null
("sweep: {0:N1}s wall, cpu {1:N2}s -> dock cpu {2:N0}% of one core" -f $el, ($cpu1-$cpu0), (($cpu1-$cpu0)/$el*100))
# compare: hover should raise brightness near x=850 (magnified colorful icon) vs rest
function BandAvg($path, $xc, $halfw, $y0, $y1) {
    $bmp = New-Object System.Drawing.Bitmap($path)
    $sum = 0.0; $n = 0
    for ($x = $xc-$halfw; $x -lt $xc+$halfw; $x += 4) {
        for ($yy = $y0; $yy -lt $y1; $yy += 4) {
            if ($x -ge 0 -and $x -lt $bmp.Width -and $yy -ge 0 -and $yy -lt $bmp.Height) {
                $c = $bmp.GetPixel($x, $yy); $sum += ($c.R+$c.G+$c.B)/3.0; $n++
            }
        }
    }
    $bmp.Dispose()
    [math]::Round($sum/[math]::Max(1,$n),1)
}
$r = BandAvg 'C:\Users\ASUS\ZCodeProject\desktop_beautify\dock_rest.png' 850 40 1008 1080
$h = BandAvg 'C:\Users\ASUS\ZCodeProject\desktop_beautify\dock_hover.png' 850 40 1008 1080
"icon band brightness rest=$r hover=$h (hover should differ clearly = magnification active)"
