$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @"
using System;using System.Runtime.InteropServices;
public class CLK {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
}
"@
# reveal desktop so dock shows
$sh = New-Object -ComObject Shell.Application
$sh.MinimizeAll()
Start-Sleep -Seconds 2

function FgProc {
    $fg = [CLK]::GetForegroundWindow()
    if ($fg -eq [IntPtr]::Zero) { return '(none)' }
    $p = 0; [CLK]::GetWindowThreadProcessId($fg, [ref]$p) | Out-Null
    return (Get-Process -Id $p -ErrorAction SilentlyContinue).ProcessName
}

# screenshot and find the green WeChat icon inside the dock band
$b = [System.Windows.Forms.SystemInformation]::VirtualScreen
$bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.X, $b.Y, 0, 0, $bmp.Size)
$greenCols = @{}
for ($y = 1005; $y -lt 1068; $y += 2) {
    for ($x = 200; $x -lt 1700; $x += 2) {
        $c = $bmp.GetPixel($x, $y)
        if ($c.G -gt 120 -and $c.G -gt ($c.R + 45) -and $c.G -gt ($c.B + 30)) {
            if (-not $greenCols.ContainsKey($x)) { $greenCols[$x] = 0 }
            $greenCols[$x]++
        }
    }
}
$g.Dispose(); $bmp.Dispose()
$cols = $greenCols.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 20
if (-not $cols) { "no green icon found in dock band"; exit }
# cluster: average x of columns with high counts
$top = @($cols | Where-Object { $_.Value -ge 4 })
if ($top.Count -eq 0) { $top = @($cols | Select-Object -First 5) }
$sum = 0; foreach ($t in $top) { $sum += $t.Key }
$cx = [int]($sum / $top.Count)
"wechat icon cluster at x=$cx (green columns: $($top.Count))"
# park cursor over the icon area, wait for magnification to settle, THEN
# re-locate the green icon (layout shifts while magnifying) and click there
[CLK]::SetCursorPos($cx, $cy) | Out-Null
Start-Sleep -Milliseconds 600
$b2 = [System.Windows.Forms.SystemInformation]::VirtualScreen
$bmp2 = New-Object System.Drawing.Bitmap($b2.Width, $b2.Height)
$g2 = [System.Drawing.Graphics]::FromImage($bmp2)
$g2.CopyFromScreen($b2.X, $b2.Y, 0, 0, $bmp2.Size)
$green2 = @{}
for ($y = 960; $y -lt 1068; $y += 2) {
    for ($x = 300; $x -lt 1700; $x += 2) {
        $c = $bmp2.GetPixel($x, $y)
        if ($c.G -gt 120 -and $c.G -gt ($c.R + 45) -and $c.G -gt ($c.B + 30)) {
            if (-not $green2.ContainsKey($x)) { $green2[$x] = 0 }
            $green2[$x]++
        }
    }
}
$g2.Dispose(); $bmp2.Dispose()
$top2 = @($green2.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 12 | Where-Object { $_.Value -ge 3 })
if ($top2.Count -gt 0) {
    $s2 = 0; foreach ($t in $top2) { $s2 += $t.Key }
    $cx = [int]($s2 / $top2.Count)
    "icon after settle at x=$cx"
} else { "re-locate failed, using rest position x=$cx" }

"foreground before click: $(FgProc)"

# real click at icon center
[CLK]::SetCursorPos($cx, $cy) | Out-Null
Start-Sleep -Milliseconds 250
[CLK]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)   # LEFTDOWN
Start-Sleep -Milliseconds 60
[CLK]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)   # LEFTUP
Start-Sleep -Milliseconds 1200
$after = FgProc
"foreground after click: $after"
if ($after -in @('Weixin','WeChat')) { "E2E PASS: WeChat activated from dock click" }
else { "E2E FAIL (foreground=$after)" }
[CLK]::SetCursorPos(960, 300) | Out-Null
