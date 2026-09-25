$dock = 'C:\Users\ASUS\AppData\Local\TechRainWallpaper\dockApps'
$src2 = 'C:\Users\Public\Desktop'
foreach ($n in @('微信.lnk','QQ.lnk','网易云音乐.lnk','腾讯会议.lnk','Typora.lnk')) {
    $p = Join-Path $src2 $n
    if (Test-Path $p) { Copy-Item $p (Join-Path $dock $n) -Force; "copied $n" } else { "MISSING $n" }
}
"total: " + (@(Get-ChildItem $dock -Filter *.lnk).Count)
