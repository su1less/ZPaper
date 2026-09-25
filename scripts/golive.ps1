$ErrorActionPreference = 'Continue'
# 1. regenerate static fallback wallpaper with the new city theme
& 'C:\Users\ASUS\AppData\Local\TechRainWallpaper\TechRainWallpaper.exe' --snapshot 1 'C:\Users\ASUS\Pictures\TechRainWallpaper_bg.png'
Start-Sleep -Seconds 2
Set-ItemProperty 'HKCU:\Control Panel\Desktop' WallpaperStyle 10
Set-ItemProperty 'HKCU:\Control Panel\Desktop' TileWallpaper 0
Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public class WP2 { [DllImport("user32.dll", SetLastError=true)] public static extern bool SystemParametersInfo(int a, int b, string c, int d); }'
[WP2]::SystemParametersInfo(20, 0, 'C:\Users\ASUS\Pictures\TechRainWallpaper_bg.png', 3) | Out-Null
"static wallpaper updated"
# 2. launch v2
Start-Process 'C:\Users\ASUS\AppData\Local\TechRainWallpaper\TechRainWallpaper.exe'
Start-Sleep -Seconds 10
# 3. verify
$p = Get-Process TechRainWallpaper -ErrorAction SilentlyContinue
if ($p) {
    $p.Refresh()
    ("proc: PID {0}, memory {1:N1} MB, cpu {2:N2}s" -f $p.Id, ($p.WorkingSet64/1MB), $p.TotalProcessorTime.TotalSeconds)
} else { "PROC NOT RUNNING" }
