$ErrorActionPreference = 'Continue'
Add-Type -TypeDefinition @"
using System;using System.Text;using System.Runtime.InteropServices;
public class ACT {
    public delegate bool EP(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EP cb, IntPtr l);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x,int y,int w,int ht, uint flags);
}
"@
# find candidate windows: visible + titled, not us, not desktop
$self = [System.Diagnostics.Process]::GetCurrentProcess().Id
$cands = New-Object System.Collections.ArrayList
$cb = [ACT+EP]{ param($h, $l)
    $p = 0; [ACT]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
    if ([ACT]::IsWindowVisible($h) -and [ACT]::GetWindowTextLength($h) -gt 0 -and $p -ne $script:self) {
        $sb = New-Object System.Text.StringBuilder 256
        [ACT]::GetWindowText($h, $sb, 256) | Out-Null
        $title = $sb.ToString()
        if ($title -notmatch '^$') {
            $pn = (Get-Process -Id $p -ErrorAction SilentlyContinue).ProcessName
            if ($pn -and $pn -notin @('ApplicationFrameHost','explorer','SearchHost','TextInputHost','dllhost','sihost')) {
                [void]$script:cands.Add([pscustomobject]@{H=$h; Pid=$p; Proc=$pn; Title=$title})
            }
        }
    }
    return $true
}
$script:self = $self; $script:cands = $cands
[ACT]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
"candidate windows: "
$cands | Select-Object -First 8 | ForEach-Object { "  {0} (pid {1}) '{2}'" -f $_.Proc, $_.Pid, $_.Title.Substring(0, [math]::Min(30, $_.Title.Length)) }
if ($cands.Count -eq 0) { "no candidates"; exit }

$t = $cands | Where-Object { $_.Proc -in @('QQ','Weixin','WeChat','Notepad','mspaint') } | Select-Object -First 1
if (-not $t) { $t = $cands[0] }
"target: {0} hwnd={1}" -f $t.Proc, $t.H

function FgName {
    $fg = [ACT]::GetForegroundWindow()
    if ($fg -eq [IntPtr]::Zero) { return '(none)' }
    $p = 0; [ACT]::GetWindowThreadProcessId($fg, [ref]$p) | Out-Null
    return (Get-Process -Id $p -ErrorAction SilentlyContinue).ProcessName
}
"foreground before: $(FgName)"

# method 1: ALT trick + SetForegroundWindow (exact current code path)
[ACT]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
[ACT]::SetForegroundWindow($t.H) | Out-Null
[ACT]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 400
"after ALT trick: foreground = $(FgName) (want $($t.Proc))"

if ((FgName) -ne $t.Proc) {
    # method 2: minimize -> restore toggle (system-sanctioned activation)
    if (-not [ACT]::IsIconic($t.H)) { [ACT]::ShowWindow($t.H, 11) | Out-Null; Start-Sleep -Milliseconds 150 }
    [ACT]::ShowWindow($t.H, 9) | Out-Null
    Start-Sleep -Milliseconds 400
    "after min/restore: foreground = $(FgName)"
}
if ((FgName) -ne $t.Proc) {
    # method 3: HWND_TOP raise + retry
    [ACT]::SetWindowPos($t.H, [IntPtr]1, 0,0,0,0, 0x0040 -bor 0x0010 -bor 0x0001 -bor 0x0002) | Out-Null
    [ACT]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
    [ACT]::SetForegroundWindow($t.H) | Out-Null
    [ACT]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 400
    "after raise+retry: foreground = $(FgName)"
}
