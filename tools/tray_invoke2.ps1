param([string]$namePart)
$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class TB6 {
    [DllImport("user32.dll")] public static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out TB6.P p);
    [StructLayout(LayoutKind.Sequential)] public struct P { public int X, Y; }
}
"@
$tb = [TB6]::FindWindow('Shell_TrayWnd', $null)
if ($tb -eq [IntPtr]::Zero) { Write-Host 'no taskbar'; exit 1 }
[TB6]::ShowWindow($tb, 5) | Out-Null
Start-Sleep -Milliseconds 900
try { [TB6]::ShowWindow($tb, 5) | Out-Null } catch {}

# fast: UIA element straight from the taskbar handle (small subtree)
$trayEl = [System.Windows.Automation.AutomationElement]::FromHandle($tb)
if (-not $trayEl) { Write-Host 'no uia for taskbar'; [TB6]::ShowWindow($tb, 0) | Out-Null; exit 1 }

# find chevron by AutomationId (fast, scoped)
$chev = $null
try {
    $c1 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'ShowHiddenIcons')
    $chev = $trayEl.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c1)
} catch { Write-Host ('chevron search err ' + $_.Exception.Message) }
if (-not $chev) {
    # fallback: name-based
    $all = $trayEl.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($e in $all) { if ($e.Current.AutomationId -eq 'ShowHiddenIcons') { $chev = $e; break } }
    Write-Host ('taskbar subtree size: ' + $all.Count)
}
if (-not $chev) { Write-Host 'NO chevron'; [TB6]::ShowWindow($tb, 0) | Out-Null; exit 2 }
$cr = $chev.Current.BoundingRectangle
Write-Host ('chevron rect=' + $cr)
$ccx = [int](($cr.Left + $cr.Right) / 2); $ccy = [int](($cr.Top + $cr.Bottom) / 2)
$o = New-Object TB6+P; [TB6]::GetCursorPos([ref]$o) | Out-Null
[TB6]::SetCursorPos($ccx, $ccy) | Out-Null; Start-Sleep -Milliseconds 200
[TB6]::mouse_event(2,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 60; [TB6]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
Start-Sleep -Milliseconds 900
Write-Host 'chevron clicked'

# overflow window by class name (Win11)
$ov = [TB6]::FindWindow('TopLevelWindowForOverflowXamlIsland', $null)
Write-Host ('overflow hwnd=' + $ov)
$icon = $null
if ($ov -ne [IntPtr]::Zero) {
    $ovEl = [System.Windows.Automation.AutomationElement]::FromHandle($ov)
    $n2 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $namePart)
    $icon = $ovEl.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $n2)
    if (-not $icon) {
        $all2 = $ovEl.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
        Write-Host ('overflow subtree ' + $all2.Count)
        foreach ($e in $all2) { if ($e.Current.Name -like ("*" + $namePart + "*")) { $icon = $e; break } }
    }
} else {
    # fallback: search taskbar subtree for the icon directly (some icons are promoted)
    $n3 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $namePart)
    $icon = $trayEl.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $n3)
}
if (-not $icon) { Write-Host 'ICON NOT FOUND'; Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.SendKeys]::SendWait('{ESC}'); Start-Sleep -Milliseconds 200; [TB6]::ShowWindow($tb, 0) | Out-Null; exit 3 }
$ir = $icon.Current.BoundingRectangle
$icx = [int](($ir.Left + $ir.Right) / 2); $icy = [int](($ir.Top + $ir.Bottom) / 2)
Write-Host ("icon '" + $icon.Current.Name + "' rect=" + $ir)
[TB6]::SetCursorPos($icx, $icy) | Out-Null; Start-Sleep -Milliseconds 250
[TB6]::mouse_event(2,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 60; [TB6]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
Start-Sleep -Milliseconds 1200
[TB6]::SetCursorPos($o.X, $o.Y) | Out-Null
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.SendKeys]::SendWait('{ESC}')
Start-Sleep -Milliseconds 300
[TB6]::ShowWindow($tb, 0) | Out-Null
Write-Host 'DONE icon clicked, taskbar hidden'
