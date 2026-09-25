$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @"
using System;using System.Text;using System.Runtime.InteropServices;
public class ST4 {
    public delegate bool EP(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EP cb, IntPtr l);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(int x, int y);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
}
"@
"ALT key state: {0:X} (odd bit = pressed)" -f [ST4]::GetAsyncKeyState(0x12)
$targets = @{}
foreach ($n in @('Weixin','QQ','cloudmusic')) {
    $ps = @(Get-Process -Name $n -ErrorAction SilentlyContinue)
    foreach ($p in $ps) { $targets[[uint32]$p.Id] = $n }
}
"found pids: " + (($targets.GetEnumerator() | ForEach-Object { "$($_.Value)=$($_.Key)" }) -join ' ')
$script:targets = $targets
$script:rows = New-Object System.Collections.ArrayList
$cb = [ST4+EP]{ param($h, $l)
    $p = 0; [ST4]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
    if ($script:targets.ContainsKey([uint32]$p)) {
        $sb = New-Object System.Text.StringBuilder 200
        [void][ST4]::GetWindowText($h, $sb, 200)
        $r = New-Object ST4+RECT
        [void][ST4]::GetWindowRect($h, [ref]$r)
        $ex = [int64][ST4]::GetWindowLongPtr($h, -20)
        $w = $r.R - $r.L; $ht = $r.B - $r.T
        if (($sb.ToString().Length -gt 0 -or ($w -gt 50 -and $ht -gt 50)) -and $w -gt 0) {
            [void]$script:rows.Add("proc={0} vis={1} enabled={2} {3}x{4} pos=({5},{6}) ex(L={7},T={8}) title='{9}'" -f `
                $script:targets[[uint32]$p], [ST4]::IsWindowVisible($h), [ST4]::IsWindowEnabled($h), $w, $ht, $r.L, $r.T,
                (($ex -band 0x80000) -ne 0), (($ex -band 0x20) -ne 0), $sb.ToString())
        }
    }
    return $true
}
[ST4]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
$script:rows | ForEach-Object { "  $_" }
# who receives clicks at screen center (typical main-window area)?
foreach ($pt in @(@(960,500), @(700,300))) {
    $h = [ST4]::WindowFromPoint($pt[0], $pt[1])
    $p = 0; [ST4]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
    $pn = (Get-Process -Id $p -ErrorAction SilentlyContinue).ProcessName
    "WindowFromPoint({0},{1}): {2} ({3})" -f $pt[0], $pt[1], $pn, $h
}
