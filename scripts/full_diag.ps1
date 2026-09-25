$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @"
using System;using System.Text;using System.Runtime.InteropServices;
public class DIAG {
    public delegate bool EP(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EP cb, IntPtr l);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint data, UIntPtr extra);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
}
"@
function FgInfo {
    $fg = [DIAG]::GetForegroundWindow()
    if ($fg -eq [IntPtr]::Zero) { return '(none)' }
    $p = 0; [DIAG]::GetWindowThreadProcessId($fg, [ref]$p) | Out-Null
    $sb = New-Object System.Text.StringBuilder 200
    [void][DIAG]::GetWindowText($fg, $sb, 200)
    $pr = Get-Process -Id $p -ErrorAction SilentlyContinue
    return "pid={0} proc={1} start={2} title='{3}'" -f $p, $pr.ProcessName, ($pr.StartTime.ToString('HH:mm:ss')), $sb.ToString()
}

function DiagApp($procName, $label) {
    "==================================================================="
    "=== $label ($procName) ==="
    $procs = @(Get-Process -Name $procName -ErrorAction SilentlyContinue | Sort-Object StartTime)
    if ($procs.Count -eq 0) { "  no processes"; return }
    $firstPid = $procs[0].Id
    foreach ($pr in $procs) { "  pid {0}  start {1:HH:mm:ss}  main='{2}'" -f $pr.Id, $pr.StartTime, $pr.MainWindowTitle }
    "  firstPid (earliest) = $firstPid"
    # collect windows >= 100px
    $script:pids = $procs.Id
    $script:winRows = New-Object System.Collections.ArrayList
    $cb = [DIAG+EP]{ param($h, $l)
        $p = 0; [DIAG]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
        if ($script:pids -contains [int]$p) {
            $r = New-Object DIAG+RECT
            [void][DIAG]::GetWindowRect($h, [ref]$r)
            $w = $r.R - $r.L; $ht = $r.B - $r.T
            if ($w -ge 100 -and $ht -ge 80) {
                $sb = New-Object System.Text.StringBuilder 200
                [void][DIAG]::GetWindowText($h, $sb, 200)
                $ex = [int64][DIAG]::GetWindowLongPtr($h, -20)
                [void]$script:winRows.Add([pscustomobject]@{
                    H=$h; Pid=$p; W=$w; Ht=$ht; Vis=[DIAG]::IsWindowVisible($h);
                    Title=$sb.ToString(); Layered=(($ex -band 0x80000) -ne 0); Transp=(($ex -band 0x20) -ne 0)})
            }
        }
        return $true
    }
    [DIAG]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
    # v17 scoring replication
    $screenArea = 1920*1080
    foreach ($wr in $script:winRows) {
        $area = [long]$wr.W * $wr.Ht
        $dummy = ($wr.Layered -and $wr.Transp) -or ($area -gt $screenArea*11/10)
        $score = 0L
        if ($wr.Pid -eq $firstPid) { $score += 3000000 }
        if ($wr.Vis) { $score += 150000 }
        if ($wr.Title.Length -gt 0) { $score += 200000 }
        $score += [math]::Min($area, $screenArea)
        if ($dummy) { $score -= 2000000000 }
        $wr | Add-Member -NotePropertyName Score -NotePropertyValue $score
    }
    "  windows ranked (v17 scoring):"
    $script:winRows | Sort-Object Score -Descending | Select-Object -First 6 | ForEach-Object {
        "    pid={0} vis={1} {2}x{3} score={4} title='{5}'" -f $_.Pid, $_.Vis, $_.W, $_.Ht, $_.Score, $_.Title.Substring(0,[math]::Min(28,$_.Title.Length))
    }
}

DiagApp 'Weixin' '微信'
DiagApp 'QQ' 'QQ'
DiagApp 'cloudmusic' '网易云音乐'

# ============ REAL CLICK TEST ============
"==================================================================="
"=== real click test ==="
$sh = New-Object -ComObject Shell.Application
$sh.MinimizeAll()
Start-Sleep -Seconds 2
"foreground now: $(FgInfo)"

function FindIcon($name, $test) {
    $b = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($b.X, $b.Y, 0, 0, $bmp.Size)
    $cols = @{}
    for ($y = 1005; $y -lt 1068; $y += 2) {
        for ($x = 200; $x -lt 1700; $x += 2) {
            $c = $bmp.GetPixel($x, $y)
            if (& $test $c) { if (-not $cols.ContainsKey($x)) { $cols[$x]=0 }; $cols[$x]++ }
        }
    }
    $g.Dispose(); $bmp.Dispose()
    $top = @($cols.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 14 | Where-Object { $_.Value -ge 3 })
    if ($top.Count -eq 0) { return -1 }
    $s = 0; foreach ($t in $top) { $s += $t.Key }
    return [int]($s / $top.Count)
}

# WeChat: green icon
$green = { param($c) $c.G -gt 120 -and $c.G -gt ($c.R + 45) -and $c.G -gt ($c.B + 30) }
$x = FindIcon 'wechat' $green
if ($x -gt 0) {
    # settle: hover first, re-locate after magnification shifts layout
    [DIAG]::SetCursorPos($x, 1040) | Out-Null
    Start-Sleep -Milliseconds 500
    $x2 = FindIcon 'wechat2' $green
    if ($x2 -gt 0) { $x = $x2 }
    "clicking WeChat icon at x=$x"
    [DIAG]::SetCursorPos($x, 1040) | Out-Null
    Start-Sleep -Milliseconds 200
    [DIAG]::mouse_event(2,0,0,0,[UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [DIAG]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
    Start-Sleep -Milliseconds 1500
    "1.5s after WeChat click, foreground: $(FgInfo)"
} else { "WeChat icon not found" }
[DIAG]::SetCursorPos(960, 300) | Out-Null
