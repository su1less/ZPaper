$ErrorActionPreference = 'Continue'
Add-Type -TypeDefinition @"
using System;using System.Text;using System.Runtime.InteropServices;
public class WPX {
    public delegate bool EP(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EP cb, IntPtr l);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(int x, int y);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
}
"@
"=== who receives clicks right now ==="
foreach ($pt in @(@(700,500), @(960,500), @(1200,500), @(700,300), @(960,800), @(600,1040))) {
    $h = [WPX]::WindowFromPoint($pt[0], $pt[1])
    $p = 0; [WPX]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
    $pn = (Get-Process -Id $p -EA SilentlyContinue).ProcessName
    $sb = New-Object System.Text.StringBuilder 100; [void][WPX]::GetWindowText($h, $sb, 100)
    $cn = New-Object System.Text.StringBuilder 100; [void][WPX]::GetClassName($h, $cn, 100)
    "({0},{1}) -> {2} ({3}) cls='{4}' title='{5}'" -f $pt[0], $pt[1], $pn, $h, $cn.ToString(), $sb.ToString()
}
"=== foreground ==="
$fg = [WPX]::GetForegroundWindow()
$p = 0; [WPX]::GetWindowThreadProcessId($fg, [ref]$p) | Out-Null
"fg pid {0} = {1}" -f $p, (Get-Process -Id $p -EA SilentlyContinue).ProcessName
"=== any VISIBLE window of Weixin/QQ/cloudmusic covering most of the screen ==="
$targets = @{}
foreach ($n in @('QQ','Weixin','WeChatAppEx','cloudmusic')) {
    foreach ($pr in @(Get-Process -Name $n -EA SilentlyContinue)) { $targets[[uint32]$pr.Id] = $n }
}
$script:targets = $targets
$cb = [WPX+EP]{ param($h, $l)
    $p = 0; [WPX]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
    if ($script:targets.ContainsKey([uint32]$p) -and [WPX]::IsWindowVisible($h)) {
        $r = New-Object WPX+RECT; [void][WPX]::GetWindowRect($h, [ref]$r)
        $w = $r.R-$r.L; $ht = $r.B-$r.T
        if ($w -ge 1300 -and $ht -ge 700) {
            $sb = New-Object System.Text.StringBuilder 100; [void][WPX]::GetWindowText($h, $sb, 100)
            $cn = New-Object System.Text.StringBuilder 100; [void][WPX]::GetClassName($h, $cn, 100)
            $ex = [int64][WPX]::GetWindowLongPtr($h, -20)
            "proc={0} hwnd={1} {2}x{3} pos=({4},{5}) layered={6} transp={7} cls='{8}' title='{9}'" -f `
                $script:targets[[uint32]$p], $h, $w, $ht, $r.L, $r.T, (($ex -band 0x80000) -ne 0), (($ex -band 0x20) -ne 0), $cn.ToString(), $sb.ToString()
        }
    }
    return $true
}
[WPX]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
