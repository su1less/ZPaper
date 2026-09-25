$ErrorActionPreference = 'Continue'
Add-Type -TypeDefinition @"
using System;using System.Text;using System.Runtime.InteropServices;
public class CLN {
    public delegate bool EP(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EP cb, IntPtr l);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(int x, int y);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
}
"@
# hide giant LAYERED helper windows of Weixin/QQ/cloudmusic that our earlier
# activation attempts SW_SHOW'ed (invisible full-screen click eaters)
$targets = @{}
foreach ($n in @('Weixin','WeChat','QQ','cloudmusic')) {
    foreach ($p in @(Get-Process -Name $n -ErrorAction SilentlyContinue)) { $targets[[uint32]$p.Id] = $n }
}
$script:targets = $targets
$script:cleaned = 0
$cb = [CLN+EP]{ param($h, $l)
    $p = 0; [CLN]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
    if ($script:targets.ContainsKey([uint32]$p) -and [CLN]::IsWindowVisible($h)) {
        $r = New-Object CLN+RECT
        [void][CLN]::GetWindowRect($h, [ref]$r)
        $w = $r.R - $r.L; $ht = $r.B - $r.T
        $ex = [int64][CLN]::GetWindowLongPtr($h, -20)
        $layered = ($ex -band 0x80000) -ne 0
        $sb = New-Object System.Text.StringBuilder 200
        [void][CLN]::GetWindowText($h, $sb, 200)
        $title = $sb.ToString()
        # full-screen-ish AND layered helper (the main windows are not full screen)
        if ($layered -and $w -ge 1500 -and $ht -ge 900) {
            [void][CLN]::ShowWindow($h, 0)
            $script:cleaned++
            "hidden: {0} hwnd={1} {2}x{3} '{4}'" -f $script:targets[[uint32]$p], $h, $w, $ht, $title
        }
    }
    return $true
}
[CLN]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
"cleaned $script:cleaned overlay window(s)"
Start-Sleep -Milliseconds 400
foreach ($pt in @(@(960,500), @(700,300), @(400,200))) {
    $h = [CLN]::WindowFromPoint($pt[0], $pt[1])
    $p = 0; [CLN]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
    $pn = (Get-Process -Id $p -ErrorAction SilentlyContinue).ProcessName
    "WindowFromPoint({0},{1}): {2}" -f $pt[0], $pt[1], $pn
}
