# Drives the ribbon with UI Automation: picks the Modeled Threads tab, clicks
# Create Thread, and reports whether the dialog came up.
#
# The screen is 160% scaled, so this process is made per-monitor DPI aware
# first: that way UI Automation's rectangles and SetCursorPos both talk in real
# screen pixels, instead of one in logical pixels and the other in physical.

$log = 'C:\Temp\ui-flow5.txt'
"starting" | Out-File $log -Encoding utf8

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class Win32
{
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, int extra);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern uint GetDpiForSystem();

    public const uint LeftDown = 0x0002;
    public const uint LeftUp = 0x0004;

    // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
    public static readonly IntPtr PerMonitorAwareV2 = new IntPtr(-4);

    public static bool MakeDpiAware() { return SetProcessDpiAwarenessContext(PerMonitorAwareV2); }
}
'@

function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }

$aware = [Win32]::MakeDpiAware()
Start-Sleep -Milliseconds 300
Note "dpi aware: $aware, system dpi: $([Win32]::GetDpiForSystem()), screen: $([System.Windows.Forms.Screen]::PrimaryScreen.Bounds)"

function ClickAt([int] $x, [int] $y) {
    # Somewhere else first, so any tool tip over the target goes away; then a
    # quick press, before a new tool tip can cover it.
    [Win32]::SetCursorPos(700, 700) | Out-Null
    Start-Sleep -Milliseconds 600

    [Win32]::SetCursorPos($x, $y) | Out-Null
    Start-Sleep -Milliseconds 60

    $p = New-Object Win32+POINT
    [Win32]::GetCursorPos([ref] $p) | Out-Null
    Note "clicking at $x,$y (cursor now $($p.X),$($p.Y))"

    [Win32]::mouse_event([Win32]::LeftDown, 0, 0, 0, 0)
    Start-Sleep -Milliseconds 120
    [Win32]::mouse_event([Win32]::LeftUp, 0, 0, 0, 0)
    Start-Sleep -Milliseconds 900
}

$inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')
$doc = $inv.ActiveDocument
Note "document: $($doc.DisplayName)"

# Select the shaft's face so the pick is skipped.
$face = $null
foreach ($body in $doc.ComponentDefinition.SurfaceBodies) {
    foreach ($f in $body.Faces) {
        $radius = $null
        try { $radius = $f.Geometry.Radius } catch { continue }
        if ($null -ne $radius -and [Math]::Abs(($radius * 10) - 5.0) -lt 0.1) { $face = $f; break }
    }
    if ($null -ne $face) { break }
}

if ($null -eq $face) { Note "no cylindrical face of radius 5 mm"; exit 1 }
$doc.SelectSet.Select($face)
Note "face of radius 5 mm selected"

$root = [System.Windows.Automation.AutomationElement]::RootElement

function OnScreen([string] $name) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $name)
    $found = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
    foreach ($element in $found) {
        $rect = $element.Current.BoundingRectangle
        if (-not $element.Current.IsOffscreen -and $rect.Width -gt 1 -and $rect.Height -gt 1) {
            return $element
        }
    }

    return $null
}

$tab = OnScreen 'Modeled Threads'
if ($null -eq $tab) { Note "the Modeled Threads tab was not found on screen"; exit 1 }

$rect = $tab.Current.BoundingRectangle
Note "tab rect: $($rect.X),$($rect.Y) $($rect.Width)x$($rect.Height)"
ClickAt ([int] ($rect.X + $rect.Width / 2)) ([int] ($rect.Y + $rect.Height / 2))
Start-Sleep -Seconds 2

$button = OnScreen 'Create Thread'
if ($null -eq $button) {
    Note "the button was not found on screen after clicking the tab"
    exit 1
}

$rect = $button.Current.BoundingRectangle
Note "button rect: $($rect.X),$($rect.Y) $($rect.Width)x$($rect.Height), enabled=$($button.Current.IsEnabled)"

ClickAt ([int] ($rect.X + 24)) ([int] ($rect.Y + $rect.Height - 18))
Start-Sleep -Seconds 5

$dialog = OnScreen 'Create a Modelled Thread'
if ($null -eq $dialog) {
    Note "the dialog did not appear"
    exit 1
}

$rect = $dialog.Current.BoundingRectangle
Note "the dialog is up: $($rect.X),$($rect.Y) $($rect.Width)x$($rect.Height)"

$texts = $dialog.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.Condition]::TrueCondition)
foreach ($element in $texts) {
    $name = $element.Current.Name
    if ([string]::IsNullOrWhiteSpace($name)) { continue }
    Note "  $($element.Current.ControlType.ProgrammaticName): $name"
}

Note "done"
