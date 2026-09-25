$ErrorActionPreference = 'Continue'
Add-Type -TypeDefinition @"
using System;using System.Text;using System.Runtime.InteropServices;
public class QQ3 {
    public delegate bool EP(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EP cb, IntPtr l);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
}
"@
"processes matching QQ:"
Get-Process | Where-Object { $_.ProcessName -match 'QQ' } | ForEach-Object { "  {0} pid={1} mainHwnd={2} title='{3}'" -f $_.ProcessName, $_.Id, $_.MainWindowHandle, $_.MainWindowTitle }
$pids = @(Get-Process | Where-Object { $_.ProcessName -match '^QQ' } | ForEach-Object { $_.Id })
"all windows of QQ pids ($($pids -join ',')):"
$script:rows = New-Object System.Collections.ArrayList
$cb = [QQ3+EP]{ param($h, $l)
    $p = 0; [QQ3]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
    if ($script:pids -contains [int]$p) {
        $sb = New-Object System.Text.StringBuilder 200
        [void][QQ3]::GetWindowText($h, $sb, 200)
        $cn = New-Object System.Text.StringBuilder 128
        [void][QQ3]::GetClassName($h, $cn, 128)
        $r = New-Object QQ3+RECT
        [void][QQ3]::GetWindowRect($h, [ref]$r)
        [void]$script:rows.Add("hwnd=$h vis=$([QQ3]::IsWindowVisible($h)) rect=$($r.L),$($r.T),$($r.R),$($r.B) cls='$($cn.ToString())' title='$($sb.ToString())'")
    }
    return $true
}
$script:pids = $pids
[QQ3]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
$script:rows | ForEach-Object { "  $_" }
