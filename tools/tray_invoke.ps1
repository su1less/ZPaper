param([string]$namePart)
$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class TB5 {
    [DllImport("user32.dll")] public static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out TB5.P p);
    [StructLayout(LayoutKind.Sequential)] public struct P { public int X, Y; }
}
"@
# 1. show taskbar
$tb = [TB5]::FindWindow('Shell_TrayWnd', $null)
if ($tb -eq [IntPtr]::Zero) { Write-Host 'no Shell_TrayWnd'; exit 1 }
[TB5]::ShowWindow($tb, 5) | Out-Null
Start-Sleep -Milliseconds 900

$root = [System.Windows.Automation.AutomationElement]::RootElement
# locate tray elements by walking ALL root children then descendants shallowly
$found = New-Object System.Collections.ArrayList
$kids = $root.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
foreach ($k in $kids) {
    $desc = $k.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($d in $desc) {
        $n = $d.Current.Name
        $aid = $d.Current.AutomationId
        if ($n -or $aid) { [void]$found.Add($d) }
    }
}
Write-Host ("collected " + $found.Count + " tray-ish elements")
# dump interesting ones
foreach ($el in $found) {
    $n = $el.Current.Name; $aid = $el.Current.AutomationId; $cls = $el.Current.ClassName
    if ($n -match 'QQ|hidden|Hidden' -or $aid -match 'ShowHiddenIcons') {
        Write-Host ("MATCH name='" + $n + "' aid=" + $aid + " cls=" + $cls + " rect=" + $el.Current.BoundingRectangle)
    }
}
# try invoke chevron
$chev = $null
foreach ($el in $found) { if ($el.Current.AutomationId -eq 'ShowHiddenIcons') { $chev = $el; break } }
if ($chev) {
    try {
        $inv = $chev.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern) -as [System.Windows.Automation.InvokePattern]
        if ($inv) { $inv.Invoke(); Write-Host 'chevron INVOKED' } else { Write-Host 'chevron no InvokePattern' }
    } catch { Write-Host ("chevron invoke failed: " + $_.Exception.Message) }
    Start-Sleep -Milliseconds 900
} else { Write-Host 'no chevron found' }

# after overflow opens, re-scan root children for the overflow window and the target icon
$kids2 = $root.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
$icon = $null
foreach ($k in $kids2) {
    $desc = $k.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($d in $desc) {
        if ($d.Current.Name -like "*$namePart*") { $icon = $d; break }
    }
    if ($icon) { break }
}
if (-not $icon) { Write-Host "icon '$namePart' NOT FOUND in overflow"; [TB5]::ShowWindow($tb, 0) | Out-Null; exit 2 }
$rect = $icon.Current.BoundingRectangle
Write-Host ("icon found: '" + $icon.Current.Name + "' rect=" + $rect)
$cx = [int](($rect.Left + $rect.Right) / 2); $cy = [int](($rect.Top + $rect.Bottom) / 2)
$clicked = $false
try {
    $inv2 = $icon.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern) -as [System.Windows.Automation.InvokePattern]
    if ($inv2) { $inv2.Invoke(); $clicked = $true; Write-Host 'icon INVOKED' }
} catch {}
if (-not $clicked) {
    $o = New-Object TB5+P; [TB5]::GetCursorPos([ref]$o) | Out-Null
    [TB5]::SetCursorPos($cx, $cy) | Out-Null
    Start-Sleep -Milliseconds 250
    [TB5]::mouse_event(2,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 60; [TB5]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
    [TB5]::SetCursorPos($o.X, $o.Y) | Out-Null
    Write-Host "icon real-clicked at ($cx,$cy)"
}
Start-Sleep -Milliseconds 1500
# press Escape to close overflow, hide taskbar again
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.SendKeys]::SendWait('{ESC}')
Start-Sleep -Milliseconds 300
[TB5]::ShowWindow($tb, 0) | Out-Null
Write-Host 'done, taskbar hidden again'
