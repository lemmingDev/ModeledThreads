# Drives the ribbon with UI Automation: picks the Modeled Threads tab, clicks
# Create Thread, and reports whether the dialog came up. Writes as it goes.

$log = 'C:\Temp\ui-flow4.txt'
"starting" | Out-File $log -Encoding utf8

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
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
    public const uint LeftDown = 0x0002;
    public const uint LeftUp = 0x0004;
}
'@

function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }

function ClickAt([int] $x, [int] $y) {
    # Somewhere else first, so any tool tip over the target goes away; then a
    # quick press, before a new tool tip can cover it.
    [Win32]::SetCursorPos(700, 700) | Out-Null
    Start-Sleep -Milliseconds 700

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

if ($null -eq $face) { Note "no face"; exit 1 }
$doc.SelectSet.Select($face)
Note "face selected"

$root = [System.Windows.Automation.AutomationElement]::RootElement

$tabCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, 'Modeled Threads')
$tab = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $tabCondition)

if ($null -eq $tab) { Note "no tab element"; exit 1 }

$rect = $tab.Current.BoundingRectangle
Note "tab rect: $($rect.X),$($rect.Y) $($rect.Width)x$($rect.Height)"

# Click the tab to bring its panel into being.
ClickAt ([int] ($rect.X + $rect.Width / 2)) ([int] ($rect.Y + $rect.Height / 2))
Start-Sleep -Seconds 2

$buttonCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, 'Create Thread')
$button = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $buttonCondition)

if ($null -eq $button) {
    Note "the button was not found after clicking the tab"

    # Dump what is under the ribbon, to see what the controls are called.
    $all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    $count = 0
    foreach ($element in $all) {
        $name = $element.Current.Name
        if ([string]::IsNullOrWhiteSpace($name)) { continue }
        if ($name -like '*Thread*' -or $element.Current.ControlType.ProgrammaticName -like '*Button*') {
            Note "  element: '$name' $($element.Current.ControlType.ProgrammaticName) rect=$($element.Current.BoundingRectangle)"
            $count++
            if ($count -gt 40) { break }
        }
    }

    exit 1
}

$buttonRect = $button.Current.BoundingRectangle
Note "button rect: $($buttonRect.X),$($buttonRect.Y) $($buttonRect.Width)x$($buttonRect.Height)"

try {
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Note "button invoked through UI Automation"
}
catch {
    Note "invoke failed: $_"

    # The tool tip hangs over the middle of the button, so click the bottom left
    # corner, which stays clear of it.
    $x = [int] ($buttonRect.X + 22)
    $y = [int] ($buttonRect.Y + $buttonRect.Height - 14)

    ClickAt $x $y
    Note "clicked the button at $x,$y"
    ClickAt $x $y
    Note "clicked the button again"
}

Start-Sleep -Seconds 6

$dialogCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, 'Create a Modelled Thread')
$dialog = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $dialogCondition)

Note ($(if ($null -eq $dialog) { "the dialog did not appear" } else { "the dialog is up" }))
Note "done"
