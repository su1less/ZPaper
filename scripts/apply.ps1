$ErrorActionPreference = 'Continue'
Start-Transcript -Path 'C:\Users\ASUS\ZCodeProject\desktop_beautify\apply.log' -Force

# ---------- 1. static fallback wallpaper ----------
Set-ItemProperty 'HKCU:\Control Panel\Desktop' WallpaperStyle 10
Set-ItemProperty 'HKCU:\Control Panel\Desktop' TileWallpaper 0
Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public class WP { [DllImport("user32.dll", SetLastError=true)] public static extern bool SystemParametersInfo(int a, int b, string c, int d); }'
$png = "$env:USERPROFILE\Pictures\TechRainWallpaper_bg.png"
$r = [WP]::SystemParametersInfo(20, 0, $png, 3)
"wallpaper api result: $r ; now = $((Get-ItemProperty 'HKCU:\Control Panel\Desktop').Wallpaper)"

# ---------- 2. archive desktop ----------
$desk = 'D:\Desktop'
$arc  = 'D:\桌面归档_2026-09-21'
$docExt  = @('.pdf','.doc','.docx','.ppt','.pptx','.xls','.xlsx','.md','.tex','.bib','.txt')
$zipExt  = @('.zip','.rar','.7z','.tar','.gz')
$imgExt  = @('.png','.jpg','.jpeg','.gif','.bmp','.svg')
$codeExt = @('.py','.yaml','.yml','.npz','.hdf5','.vesta','.vasp','.csv','.cif')
$vaspNames = @('INCAR','CONTCAR','KPOINTS')
foreach ($c in @('文件夹','文档','压缩包','图片','代码与数据','快捷方式','其他')) {
    New-Item -ItemType Directory -Force -Path (Join-Path $arc $c) | Out-Null
}
$moved = @(); $failed = @()
Get-ChildItem -LiteralPath $desk -Force | Where-Object { $_.Name -ne 'desktop.ini' -and $_.Name -notlike '.*' } | ForEach-Object {
    $e = $_.Extension.ToLower()
    $cat = '其他'
    if     ($_.PSIsContainer) { $cat = '文件夹' }
    elseif ($e -eq '.lnk')    { $cat = '快捷方式' }
    elseif ($docExt -contains $e)  { $cat = '文档' }
    elseif ($zipExt -contains $e)  { $cat = '压缩包' }
    elseif ($imgExt -contains $e)  { $cat = '图片' }
    elseif ($codeExt -contains $e -or $vaspNames -contains $_.Name -or $_.Name -like 'POSCAR*') { $cat = '代码与数据' }
    try {
        Move-Item -LiteralPath $_.FullName -Destination (Join-Path $arc $cat) -ErrorAction Stop
        $moved += "$($_.Name) -> $cat"
    } catch { $failed += $_.Name }
}
"moved: $($moved.Count)"
"failed: $($failed.Count)"
$failed | ForEach-Object { "  FAILED: $_" }

# ---------- 3. hide desktop icons ----------
New-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'HideIcons' -Value 1 -PropertyType DWord -Force | Out-Null
"HideIcons set to 1"

# ---------- 4. autostart ----------
New-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'TechRainWallpaper' -Value 'C:\Users\ASUS\AppData\Local\TechRainWallpaper\TechRainWallpaper.exe' -PropertyType String -Force | Out-Null
"autostart registered"

# ---------- 5. restart explorer ----------
Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3
if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer.exe }
Start-Sleep -Seconds 4
"explorer restarted: $((Get-Process explorer -ErrorAction SilentlyContinue).Count) process(es)"

# ---------- 6. launch animated wallpaper ----------
Start-Process 'C:\Users\ASUS\AppData\Local\TechRainWallpaper\TechRainWallpaper.exe'
Start-Sleep -Seconds 8

# ---------- 7. verify ----------
$p = Get-Process TechRainWallpaper -ErrorAction SilentlyContinue
if ($p) {
    $p.Refresh()
    ("wallpaper proc: PID {0}, memory {1:N1} MB, cpu {2:N2}s" -f $p.Id, ($p.WorkingSet64/1MB), $p.TotalProcessorTime.TotalSeconds)
} else { "WALLPAPER PROC NOT RUNNING" }
$left = @(Get-ChildItem -LiteralPath $desk -Force | Where-Object { $_.Name -ne 'desktop.ini' })
"leftover on desktop: $($left.Count)"
$left | ForEach-Object { "  leftover: $($_.Name)" }
Get-ChildItem $arc | ForEach-Object { "archive {0}: {1} items" -f $_.Name, @(Get-ChildItem -LiteralPath $_.FullName).Count }
Stop-Transcript
