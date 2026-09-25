$ErrorActionPreference = 'Continue'
Add-Type -TypeDefinition @"
using System;using System.Text;using System.Runtime.InteropServices;
public class QCHK {
    [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsHungAppWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(int x, int y);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool SetFocus(IntPtr h);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
}
"@
$h = [IntPtr]8591256   # QQ main window
$r = New-Object QCHK+RECT; [void][QCHK]::GetWindowRect($h, [ref]$r)
$cx = [int](($r.L + $r.R) / 2); $cy = [int](($r.T + $r.B) / 2)
"QQ window: rect=($($r.L),$($r.T))-($($r.R),$($r.B)) enabled=$([QCHK]::IsWindowEnabled($h)) hung=$([QCHK]::IsHungAppWindow($h))"
$h2 = [QCHK]::WindowFromPoint($cx, $cy)
$p2 = 0; [QCHK]::GetWindowThreadProcessId($h2, [ref]$p2) | Out-Null
$pn2 = (Get-Process -Id $p2 -EA SilentlyContinue).ProcessName
"WindowFromPoint(center $cx,$cy) = $pn2 (hwnd $h2)"
$fg = [QCHK]::GetForegroundWindow()
$pf = 0; [QCHK]::GetWindowThreadProcessId($fg, [ref]$pf) | Out-Null
"foreground = {0} (pid {1})" -f (Get-Process -Id $pf -EA SilentlyContinue).ProcessName, $pf
# try: activate + attach + SetFocus (worker-safe sequence) and re-check
[QCHK]::keybd_event(0x12,0,0,[UIntPtr]::Zero) | Out-Null
[QCHK]::SetForegroundWindow($h) | Out-Null
[QCHK]::keybd_event(0x12,0,2,[UIntPtr]::Zero) | Out-Null
Start-Sleep -Milliseconds 300
$cur = [QCHK]::GetCurrentThreadId()
$tgt = 0; [QCHK]::GetWindowThreadProcessId($h, [ref]$tgt) | Out-Null
[QCHK]::AttachThreadInput($cur, $tgt, $true) | Out-Null
[QCHK]::SetFocus($h) | Out-Null
[QCHK]::AttachThreadInput($cur, $tgt, $false) | Out-Null
Start-Sleep -Milliseconds 300
$fg2 = [QCHK]::GetForegroundWindow()
$pf2 = 0; [QCHK]::GetWindowThreadProcessId($fg2, [ref]$pf2) | Out-Null
"after activate+SetFocus: foreground = {0} (pid {1})" -f (Get-Process -Id $pf2 -EA SilentlyContinue).ProcessName, $pf2
