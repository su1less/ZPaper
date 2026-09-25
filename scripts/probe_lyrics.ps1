$ErrorActionPreference = 'Continue'
Add-Type -TypeDefinition @"
using System;using System.Text;using System.Runtime.InteropServices;
public class PP3 {
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(int x, int y);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
}
"@
$pts = @(@(1050,1040), @(800,1040), @(600,1040), @(1300,1040))
foreach ($pt in $pts) {
    $h = [PP3]::WindowFromPoint($pt[0], $pt[1])
    $p = 0; [PP3]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
    $pn = (Get-Process -Id $p -ErrorAction SilentlyContinue).ProcessName
    $ex = [int64][PP3]::GetWindowLongPtr($h, -20)
    $transparent = ($ex -band 0x20) -ne 0
    $layered = ($ex -band 0x80000) -ne 0
    $topmost = ($ex -band 0x8) -ne 0
    $sb = New-Object System.Text.StringBuilder 128
    [void][PP3]::GetWindowText($h, $sb, 128)
    "({0},{1}) -> {2} pid={3} '{4}' exStyle: transparent={5} layered={6} topmost={7}" -f $pt[0], $pt[1], $h, $pn, $sb.ToString(), $transparent, $layered, $topmost
}
# cloudmusic window rects
"cloudmusic windows:"
$cpid = (Get-Process cloudmusic -ErrorAction SilentlyContinue).Id
Get-Process cloudmusic -ErrorAction SilentlyContinue | ForEach-Object {
    $r = New-Object PP3+RECT
    [void][PP3]::GetWindowRect($_.MainWindowHandle, [ref]$r)
    "  pid {0} main rect {1},{2},{3},{4}" -f $_.Id, $r.L, $r.T, $r.R, $r.B
}
