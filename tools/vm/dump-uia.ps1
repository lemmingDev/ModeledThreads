# Lists what UI Automation can see of Inventor's window, trimmed to the ribbon,
# so the real names of the tab and the button can be read.

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class Win32
{
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
}
'@

[Win32]::SetProcessDpiAwarenessContext([IntPtr] (-4)) | Out-Null

$log = 'C:\Temp\uia.txt'
"starting" | Out-File $log -Encoding utf8
function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }

Note ("screen {0} x {1}" -f [Win32]::GetSystemMetrics(0), [Win32]::GetSystemMetrics(1))

$root = [System.Windows.Automation.AutomationElement]::RootElement
$condition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ClassNameProperty, 'AfxMDIFrame140u')
$frame = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)

if ($null -eq $frame) { Note "no Inventor window"; exit 1 }

Note ("frame: {0} {1}" -f $frame.Current.Name, $frame.Current.BoundingRectangle)

$all = $frame.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.Condition]::TrueCondition)
Note "elements: $($all.Count)"

$count = 0
foreach ($element in $all) {
    $rect = $element.Current.BoundingRectangle
    $name = $element.Current.Name
    $type = $element.Current.ControlType.ProgrammaticName

    # The ribbon only: the top strip of the window.
    if ($rect.Y -gt 190) { continue }
    if (-not $name -and $type -ne 'ControlType.Pane') { continue }

    Note ("{0,-28} {1,-10} {2}" -f $type, ("{0:0},{1:0} {2:0}x{3:0}" -f $rect.X, $rect.Y, $rect.Width, $rect.Height), $name)
    $count++
    if ($count -gt 200) { break }
}

Note "listed: $count"
