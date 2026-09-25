$ErrorActionPreference = 'Continue'
Add-Type -TypeDefinition @"
using System;using System.Text;using System.Runtime.InteropServices;
public class STY {
    public delegate bool EP(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EP cb, IntPtr l);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
}
"@
$targets = @{}
foreach ($n in @('QQ','Weixin','WeChatAppEx','cloudmusic')) {
    foreach ($pr in @(Get-Process -Name $n -EA SilentlyContinue)) { $targets[[uint32]$pr.Id] = $n }
}
$script:targets = $targets
"=== window styles: appwindow / layered / toolwindow / owner ==="
$cb = [STY+EP]{ param($h, $l)
    $p = 0; [STY]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
    if ($script:targets.ContainsKey([uint32]$p)) {
        $r = New-Object STY+RECT; [void][STY]::GetWindowRect($h, [ref]$r)
        $w = $r.R-$r.L; $ht = $r.B-$r.T
        if ($w -ge 400 -and $ht -ge 300) {
            $sb = New-Object System.Text.StringBuilder 120; [void][STY]::GetWindowText($h, $sb, 120)
            $cn = New-Object System.Text.StringBuilder 100; [void][STY]::GetClassName($h, $cn, 100)
            $ex = [int64][STY]::GetWindowLongPtr($h, -20)
            $owner = [STY]::GetWindow($h, 4)
            "proc={0} hwnd={1} {2}x{3} vis={4} APPWINDOW={5} LAYERED={6} TOOLW={7} EXTOOL={8} owner0={9} cls='{10}' title='{11}'" -f `
                $script:targets[[uint32]$p], $h, $w, $ht, [STY]::IsWindowVisible($h),
                (($ex -band 0x40000) -ne 0), (($ex -band 0x80000) -ne 0), (($ex -band 0x80) -ne 0),
                (($ex -band 0x8000000) -ne 0), ($owner -eq [IntPtr]::Zero), $cn.ToString(), $sb.ToString()
        }
    }
    return $true
}
[STY]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
"=== hide the black host windows we wrongly showed ==="
foreach ($t in @(@(2229016,'QQ host'), @(467468,'WeChat JettSu(leave)'))) {
    $h = [IntPtr]$t[0]
    $vis = [STY]::IsWindowVisible($h)
    "hwnd {0} ({1}) currently visible={2}" -f $h, $t[1], $vis
}
# hide 2229016 only if it is the QQ black host (untitled Chrome_WidgetWin_0)
$h = [IntPtr]2229016
$sb = New-Object System.Text.StringBuilder 100; [void][STY]::GetWindowText($h, $sb, 100)
$cn = New-Object System.Text.StringBuilder 100; [void][STY]::GetClassName($h, $cn, 100)
if ($sb.ToString().Length -eq 0 -and $cn.ToString() -like 'Chrome_WidgetWin*') {
    [void][STY]::ShowWindow($h, 0)
    "HID black QQ host 2229016"
}
