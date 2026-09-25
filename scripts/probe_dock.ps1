$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @"
using System;using System.Text;using System.Runtime.InteropServices;
public class WEnum {
    public delegate bool EP(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EP cb, IntPtr l);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
}
"@
$target = (Get-Process TechRainWallpaper).Id
$found = @()
$cb = [WEnum+EP]{ param($h, $l)
    $p = 0; [WEnum]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
    if ($p -eq $target -and [WEnum]::IsWindowVisible($h)) {
        $sb = New-Object System.Text.StringBuilder 128
        [WEnum]::GetClassName($h, $sb, 128) | Out-Null
        $r = New-Object WEnum+RECT
        [WEnum]::GetWindowRect($h, [ref]$r) | Out-Null
        $script:found += ('cls={0} rect=({1},{2})-({3},{4}) {5}x{6}' -f $sb.ToString(), $r.L, $r.T, $r.R, $r.B, $r.R-$r.L, $r.B-$r.T)
    }
    return $true
}
[WEnum]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
"visible top-level windows of our process:"
$found | ForEach-Object { "  $_" }
# sample pixels along the dock band
$bmp = New-Object System.Drawing.Bitmap('C:\Users\ASUS\ZCodeProject\desktop_beautify\dock_rest.png')
$row = 1035
$samples = @()
foreach ($x in 300..1600 | Where-Object { $_ % 100 -eq 0 }) {
    $c = $bmp.GetPixel($x, $row)
    $samples += ("x{0}:{1}" -f $x, [int](($c.R+$c.G+$c.B)/3))
}
$bmp.Dispose()
"pixel row y=$row`: " + ($samples -join ' ')
