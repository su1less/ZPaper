param([string]$namePart)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class TB2 {
    [DllImport("user32.dll")] public static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
}
"@
# show taskbar so tray icons are live
$tb = [TB2]::FindWindow('Shell_TrayWnd', $null)
[TB2]::ShowWindow($tb, 5) | Out-Null
Start-Sleep -Milliseconds 800

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ClassNameProperty, 'Shell_TrayWnd')
$tray = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if (-not $tray) { Write-Host 'NO tray'; exit 1 }

# walk descendants for buttons whose name matches
$all = $tray.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$hits = @()
foreach ($el in $all) {
    $n = $el.Current.Name
    if ($n -and $n -like "*$namePart*") { $hits += $el }
}
if ($hits.Count -eq 0) { Write-Host "no tray element matching '$namePart'"; exit 2 }
foreach ($h in $hits) {
    Write-Host ("found: '" + $h.Current.Name + "' class=" + $h.Current.ClassName + " type=" + $h.Current.ControlType.ProgrammaticName)
}
$inv = $null
foreach ($h in $hits) {
    $inv = $h.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern) -as [System.Windows.Automation.InvokePattern]
    if ($inv) { break }
}
if (-not $inv) { Write-Host 'no InvokePattern - trying LegacyIAccessible DoDefaultAction'; exit 3 }
$inv.Invoke()
Write-Host 'INVOKED'
Start-Sleep -Milliseconds 1500
# hide taskbar again
[TB2]::ShowWindow($tb, 0) | Out-Null
Write-Host 'taskbar hidden again'
