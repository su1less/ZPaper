$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @"
using System;using System.Text;using System.Runtime.InteropServices;
public class CM5 {
    public delegate bool EP(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EP cb, IntPtr l);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
}
"@
# list cloudmusic processes with start time
"cloudmusic processes:"
Get-Process cloudmusic -ErrorAction SilentlyContinue | Sort-Object StartTime | ForEach-Object {
    "  pid={0} start={1:HH:mm:ss} main='{2}'" -f $_.Id, $_.StartTime, $_.MainWindowTitle
}
# find the main player window '哭砂' or biggest hidden titled window
$best = $null
$cb = [CM5+EP]{ param($h, $l)
    $p = 0; [CM5]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
    $pn = (Get-Process -Id $p -EA SilentlyContinue).ProcessName
    if ($pn -eq 'cloudmusic') {
        $sb = New-Object System.Text.StringBuilder 200
        [void][CM5]::GetWindowText($h, $sb, 200)
        $t = $sb.ToString()
        $r = New-Object CM5+RECT
        [void][CM5]::GetWindowRect($h, [ref]$r)
        $w = $r.R-$r.L; $ht = $r.B-$r.T
        if ($w -gt 500 -and $ht -gt 400) {
            $script:best = @{H=$h; Title=$t; W=$w; Ht=$ht; Vis=[CM5]::IsWindowVisible($h); Iconic=[CM5]::IsIconic($h); Rect="$($r.L),$($r.T),$($r.R),$($r.B)"}
        }
    }
    return $true
}
[CM5]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
if (-not $best) { "no big window found"; exit }
"main window: '$($best.Title)' $($best.W)x$($best.Ht) vis=$($best.Vis) iconic=$($best.Iconic) rect=$($best.Rect)"

function Fg {
    $fg = [CM5]::GetForegroundWindow()
    if ($fg -eq [IntPtr]::Zero) { return '(none)' }
    $p = 0; [CM5]::GetWindowThreadProcessId($fg, [ref]$p) | Out-Null
    (Get-Process -Id $p -EA SilentlyContinue).ProcessName
}
# experiment: SW_RESTORE then foreground
[CM5]::ShowWindow($best.H, 9) | Out-Null
Start-Sleep -Milliseconds 400
$vis1 = [CM5]::IsWindowVisible($best.H)
"after SW_RESTORE: vis=$vis1 fg=$(Fg)"
# sample pixels in its rect to see if it is actually drawn
$r2 = New-Object CM5+RECT
[void][CM5]::GetWindowRect($best.H, [ref]$r2)
$b = [System.Windows.Forms.SystemInformation]::VirtualScreen
$bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.X, $b.Y, 0, 0, $bmp.Size)
$sum = 0.0; $n = 0
for ($x = $r2.L + 10; $x -lt $r2.R - 10; $x += 12) {
    for ($y = $r2.T + 10; $y -lt $r2.B - 10; $y += 12) {
        if ($x -ge 0 -and $x -lt $bmp.Width -and $y -ge 0 -and $y -lt $bmp.Height) {
            $c = $bmp.GetPixel($x, $y); $sum += ($c.R+$c.G+$c.B)/3.0; $n++
        }
    }
}
$g.Dispose(); $bmp.Dispose()
"window area avg brightness: {0:N0} (wallpaper ~20-60; a drawn app window is usually >80 or distinctly varied)" -f ($sum/[math]::Max(1,$n))
[CM5]::keybd_event(0x12,0,0,[UIntPtr]::Zero)
[CM5]::SetForegroundWindow($best.H) | Out-Null
[CM5]::keybd_event(0x12,0,2,[UIntPtr]::Zero)
Start-Sleep -Milliseconds 400
"after foreground: fg=$(Fg)"
