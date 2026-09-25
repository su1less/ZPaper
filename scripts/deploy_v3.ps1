$ErrorActionPreference = 'Continue'
$dock = 'C:\Users\ASUS\AppData\Local\TechRainWallpaper\dockApps'
New-Item -ItemType Directory -Force -Path $dock | Out-Null
$src1 = 'D:\桌面归档_2026-09-21\快捷方式'
$src2 = 'C:\Users\Public\Desktop'
foreach ($n in @('Notion.lnk','ZCode.lnk','百度网盘同步空间.lnk')) {
    $p = Join-Path $src1 $n
    if (Test-Path $p) { Copy-Item $p (Join-Path $dock $n) -Force }
}
foreach ($n in @('微信.lnk','QQ.lnk','网易云音乐.lnk','腾讯会议.lnk','Typora.lnk')) {
    $p = Join-Path $src2 $n
    if (Test-Path $p) { Copy-Item $p (Join-Path $dock $n) -Force }
}
"pinned apps: " + (@(Get-ChildItem $dock -Filter *.lnk).Count) + " lnk"
Start-Process 'C:\Users\ASUS\AppData\Local\TechRainWallpaper\TechRainWallpaper.exe'
Start-Sleep -Seconds 12
$p = Get-Process TechRainWallpaper -ErrorAction SilentlyContinue
if ($p) {
    $p.Refresh()
    ("proc: PID {0}, memory {1:N1} MB" -f $p.Id, ($p.WorkingSet64/1MB))
} else { "PROC NOT RUNNING" }
Add-Type -AssemblyName System.Windows.Forms
$wa = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$sb = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
("workarea {0}x{1} vs screen {2}x{3} -> taskbar hidden: {4}" -f $wa.Width, $wa.Height, $sb.Width, $sb.Height, ($wa.Height -eq $sb.Height))
