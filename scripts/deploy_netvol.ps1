$ErrorActionPreference = 'Continue'
Set-Location 'E:\cc_work\TechRainWallpaper'

# 1. stop the running instance (exe is locked while running)
Stop-Process -Name ZPapaer -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 900

# 2. force-kill skips the app's own taskbar restore -> re-show it
Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public class TB { [DllImport("user32.dll")] public static extern IntPtr FindWindowW(string c, string t); [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd); }'
$h = [TB]::FindWindowW('Shell_TrayWnd', $null)
if ($h -ne [IntPtr]::Zero) { [TB]::ShowWindow($h, 5) | Out-Null; "taskbar re-shown (hwnd $h)" } else { "taskbar window not found" }

# 3. swap in the new build
Copy-Item build_check.exe ZPapaer.exe -Force
"exe swapped: " + (Get-Item ZPapaer.exe).Length + " bytes"

# 4. relaunch
Start-Process 'E:\cc_work\TechRainWallpaper\ZPapaer.exe'
Start-Sleep -Seconds 10

# 5. verify
$p = Get-Process ZPapaer -ErrorAction SilentlyContinue
if ($p) { $p.Refresh(); "proc: PID {0}, mem {1:N0} MB" -f $p.Id, ($p.WorkingSet64/1MB) } else { "PROC NOT RUNNING" }
Get-Content log.txt -Tail 15
