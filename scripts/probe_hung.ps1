$ErrorActionPreference = 'Continue'
Add-Type -TypeDefinition @"
using System;using System.Text;using System.Runtime.InteropServices;
public class WX6 {
    public delegate bool EP(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EP cb, IntPtr l);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] public static extern bool IsHungAppWindow(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
}
"@
"=== specific windows from the activation log ==="
foreach ($t in @(@(2229016,'QQ untitled host'), @(467468,'WeChat Jett Su'))) {
    $h = [IntPtr]$t[0]
    $sb = New-Object System.Text.StringBuilder 200; [void][WX6]::GetWindowText($h, $sb, 200)
    $cn = New-Object System.Text.StringBuilder 128; [void][WX6]::GetClassName($h, $cn, 128)
    $r = New-Object WX6+RECT; [void][WX6]::GetWindowRect($h, [ref]$r)
    $owner = [WX6]::GetWindow($h, 4)
    $osb = New-Object System.Text.StringBuilder 200
    if ($owner -ne [IntPtr]::Zero) { [void][WX6]::GetWindowText($owner, $osb, 200) }
    "hwnd={0} ({1}): title='{2}' cls='{3}' visible={4} enabled={5} hung={6} rect=({7},{8})-({9},{10}) owner={11} ownerTitle='{12}'" -f `
        $h, $t[1], $sb.ToString(), $cn.ToString(), [WX6]::IsWindowVisible($h), [WX6]::IsWindowEnabled($h), [WX6]::IsHungAppWindow($h), $r.L, $r.T, $r.R, $r.B, $owner, $osb.ToString()
}
"=== all big windows of QQ / Weixin / WeChatAppEx with owner info ==="
$pids = @()
foreach ($n in @('QQ','Weixin','WeChatAppEx')) {
    foreach ($p in @(Get-Process -Name $n -ErrorAction SilentlyContinue)) { $pids += $p.Id }
}
$script:pids = $pids
$cb = [WX6+EP]{ param($h, $l)
    $p = 0; [WX6]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
    if ($script:pids -contains [int]$p) {
        $r = New-Object WX6+RECT; [void][WX6]::GetWindowRect($h, [ref]$r)
        $w = $r.R-$r.L; $ht = $r.B-$r.T
        if ($w -ge 400 -and $ht -ge 300) {
            $sb = New-Object System.Text.StringBuilder 200; [void][WX6]::GetWindowText($h, $sb, 200)
            $cn = New-Object System.Text.StringBuilder 128; [void][WX6]::GetClassName($h, $cn, 128)
            $owner = [WX6]::GetWindow($h, 4)
            $osb = New-Object System.Text.StringBuilder 100
            if ($owner -ne [IntPtr]::Zero) { [void][WX6]::GetWindowText($owner, $osb, 100) }
            "pid={0} {1}x{2} vis={3} en={4} hung={5} topLvl={6} title='{7}' cls='{8}' ownerTitle='{9}'" -f `
                $p, $w, $ht, [WX6]::IsWindowVisible($h), [WX6]::IsWindowEnabled($h), [WX6]::IsHungAppWindow($h), ($owner -eq [IntPtr]::Zero), $sb.ToString(), $cn.ToString(), $osb.ToString()
        }
    }
    return $true
}
[WX6]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
