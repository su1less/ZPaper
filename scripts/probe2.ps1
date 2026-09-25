$ErrorActionPreference = 'Continue'
Add-Type -TypeDefinition @"
using System;using System.Text;using System.Runtime.InteropServices;
public class WEnum2 {
    public delegate bool EP(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EP cb, IntPtr l);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
}
"@
$procs = @(Get-Process TechRainWallpaper -ErrorAction SilentlyContinue)
"instances: $($procs.Count)  ids: $($procs.Id -join ',')"
foreach ($proc in $procs) {
    $target = $proc.Id
    $wins = New-Object System.Collections.ArrayList
    $cb = [WEnum2+EP]{ param($h, $l)
        $p = 0; [WEnum2]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
        if ($p -eq $target) {
            $sb = New-Object System.Text.StringBuilder 128
            [WEnum2]::GetClassName($h, $sb, 128) | Out-Null
            $r = New-Object WEnum2+RECT
            [WEnum2]::GetWindowRect($h, [ref]$r) | Out-Null
            $vis = [WEnum2]::IsWindowVisible($h)
            [void]$script:wins.Add("pid=$target vis=$vis cls=$($sb.ToString()) rect=$($r.L),$($r.T) - $($r.R),$($r.B)  ($($r.R-$r.L)x$($r.B-$r.T))")
        }
        return $true
    }
    $script:wins = $wins
    [WEnum2]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
    $wins | ForEach-Object { "  $_" }
}
# WindowFromPoint at cursor 850,1042 - who owns input there?
Add-Type -TypeDefinition @"
using System;using System.Runtime.InteropServices;
public class WP2 {
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(int x, int y);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
}
"@
$h = [WP2]::WindowFromPoint(850, 1042)
$p = 0; [WP2]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
$pn = (Get-Process -Id $p -ErrorAction SilentlyContinue).ProcessName
"WindowFromPoint(850,1042): hwnd=$h pid=$p ($pn)"
