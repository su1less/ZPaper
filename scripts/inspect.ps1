$ErrorActionPreference = 'Continue'
"=== Desktop path ==="
[Environment]::GetFolderPath('Desktop')
"=== User desktop items ==="
Get-ChildItem ([Environment]::GetFolderPath('Desktop')) -Force | Select-Object Name, @{n='Type';e={if($_.PSIsContainer){'DIR'}else{'file'}}}, @{n='KB';e={[math]::Round($_.Length/1KB,1)}} | Format-Table -AutoSize
"=== Public desktop items (may need admin to move) ==="
Get-ChildItem 'C:\Users\Public\Desktop' -Force -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name
"=== Screen resolution ==="
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.Screen]::AllScreens | ForEach-Object { "$($_.DeviceName) $($_.Bounds.Width)x$($_.Bounds.Height) primary=$($_.Primary)" }
"=== Current theme / taskbar settings ==="
$personalize = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize'
"AppsUseLightTheme    = $((Get-ItemProperty $personalize -ErrorAction SilentlyContinue).AppsUseLightTheme)"
"SystemUsesLightTheme = $((Get-ItemProperty $personalize -ErrorAction SilentlyContinue).SystemUsesLightTheme)"
"EnableTransparency   = $((Get-ItemProperty $personalize -ErrorAction SilentlyContinue).EnableTransparency)"
"Wallpaper            = $((Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper -ErrorAction SilentlyContinue).Wallpaper)"
"WallpaperStyle       = $((Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name WallpaperStyle -ErrorAction SilentlyContinue).WallpaperStyle)"
$search = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' -ErrorAction SilentlyContinue).SearchboxTaskbarMode
"SearchboxTaskbarMode = $search"
$adv = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -ErrorAction SilentlyContinue
"TaskbarDa(widgets)   = $($adv.TaskbarDa)"
"TaskbarMn(chat)      = $($adv.TaskbarMn)"
"ShowTaskViewButton   = $($adv.ShowTaskViewButton)"
$mpf = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\People' -ErrorAction SilentlyContinue
"TaskbarAl(align)     = $($adv.TaskbarAl)"
