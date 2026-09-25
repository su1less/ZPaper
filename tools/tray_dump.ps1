$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class TB7 { [DllImport("user32.dll")] public static extern IntPtr FindWindow(string c, string n); [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd); }
"@
$tb = [TB7]::FindWindow('Shell_TrayWnd', $null)
[TB7]::ShowWindow($tb, 5) | Out-Null
Start-Sleep -Milliseconds 800
$el = [System.Windows.Automation.AutomationElement]::FromHandle($tb)
$all = $el.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$i = 0
foreach ($e in $all) {
    Write-Host ("#" + $i + " type=" + $e.Current.ControlType.ProgrammaticName + " name=[" + $e.Current.Name + "] aid=[" + $e.Current.AutomationId + "] cls=[" + $e.Current.ClassName + "] rect=" + $e.Current.BoundingRectangle)
    $i++
}
[TB7]::ShowWindow($tb, 0) | Out-Null
