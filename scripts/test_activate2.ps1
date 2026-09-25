$ErrorActionPreference = 'Continue'
Add-Type -TypeDefinition @"
using System;using System.Text;using System.Runtime.InteropServices;
public class ACT2 {
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
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
}
"@
function FgName {
    $fg = [ACT2]::GetForegroundWindow()
    if ($fg -eq [IntPtr]::Zero) { return '(none)' }
    $p = 0; [ACT2]::GetWindowThreadProcessId($fg, [ref]$p) | Out-Null
    return (Get-Process -Id $p -ErrorAction SilentlyContinue).ProcessName
}
$screenArea = 1920 * 1080

function TestApp($procName, $label) {
    "=== $label ($procName) ==="
    $procs = @(Get-Process -Name $procName -ErrorAction SilentlyContinue)
    if ($procs.Count -eq 0) { "  not running, skip"; return }
    $wins = New-Object System.Collections.ArrayList
    foreach ($pr in $procs) {
        $cb = [ACT2+EP]{ param($h, $l)
            $p = 0; [ACT2]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
            if ($p -eq $script:pr.Id -and [ACT2]::GetWindowTextLength($h) -gt 0) { [void]$script:wins.Add($h) }
            return $true
        }
        $script:pr = $pr; $script:wins = $wins
        [ACT2]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
    }
    if ($wins.Count -eq 0) { "  no titled windows at all"; return }
    # rank: visible +min(area,screen) ; dummy layered|transparent or oversize => -2e9
    $ranked = @()
    foreach ($h in $wins) {
        $r = New-Object ACT2+RECT
        if (-not [ACT2]::GetWindowRect($h, [ref]$r)) { continue }
        $w = $r.R - $r.L; $ht = $r.B - $r.T
        if ($w -le 0 -or $ht -le 0) { continue }
        $area = [long]$w * $ht
        $vis = [ACT2]::IsWindowVisible($h)
        $ex = [ACT2]::GetWindowLongPtr($h, -20)
        $layeredDummy = (([int64]$ex -band 0x80000) -ne 0) -and (([int64]$ex -band 0x20) -ne 0)
        $oversize = $area -gt ($screenArea * 11 / 10)
        $score = [long]($(if ($vis) { 150000 } else { 0 }) + 500000 + [math]::Min($area, $screenArea))
        if ($layeredDummy -or $oversize) { $score -= 2000000000 }
        $sb = New-Object System.Text.StringBuilder 200
        [ACT2]::GetWindowText($h, $sb, 200) | Out-Null
        $ranked += [pscustomobject]@{H=$h; Score=$score; Vis=$vis; W=$w; Ht=$ht; Title=$sb.ToString()}
    }
    $ranked = $ranked | Sort-Object Score -Descending
    $ranked | Select-Object -First 4 | ForEach-Object { "  cand vis={0} {1}x{2} score={3} '{4}'" -f $_.Vis, $_.W, $_.Ht, $_.Score, $_.Title.Substring(0,[math]::Min(24,$_.Title.Length)) }
    # try top 3 with verification, exactly like the app
    foreach ($c in ($ranked | Select-Object -First 3)) {
        $h = $c.H
        if (-not [ACT2]::IsWindowVisible($h)) { [ACT2]::ShowWindow($h, 5) | Out-Null }
        if ([ACT2]::IsIconic($h)) { [ACT2]::ShowWindow($h, 9) | Out-Null }
        [ACT2]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
        [ACT2]::SetForegroundWindow($h) | Out-Null
        [ACT2]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 300
        if ((FgName) -eq $procName) { "  ACTIVATED via direct ('{0}')" -f $c.Title.Substring(0,[math]::Min(24,$c.Title.Length)); return }
        if (-not [ACT2]::IsIconic($h)) { [ACT2]::ShowWindow($h, 11) | Out-Null; Start-Sleep -Milliseconds 100 }
        [ACT2]::ShowWindow($h, 9) | Out-Null
        Start-Sleep -Milliseconds 300
        if ((FgName) -eq $procName) { "  ACTIVATED via min/restore ('{0}')" -f $c.Title.Substring(0,[math]::Min(24,$c.Title.Length)); return }
    }
    "  FAILED to bring to foreground"
}

TestApp 'Weixin' '微信'
TestApp 'QQ' 'QQ'
TestApp 'cloudmusic' '网易云音乐'
