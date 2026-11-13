# Clicks the Create Thread button the way a user would: on the ribbon, with the
# mouse. The face is selected first through COM so the dialog has something to
# work on, and whether the dialog opens is what proves the button is wired.

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class Win32
{
    public delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, int extra);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    public const uint LeftDown = 0x0002;
    public const uint LeftUp = 0x0004;

    public static void Click(int x, int y)
    {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(250);
        mouse_event(LeftDown, 0, 0, 0, 0);
        mouse_event(LeftUp, 0, 0, 0, 0);
    }

    /// <summary>Whether a visible window with this exact title exists.</summary>
    public static bool WindowExists(string title)
    {
        bool found = false;

        EnumWindows((hwnd, lParam) =>
        {
            var text = new StringBuilder(512);
            GetWindowText(hwnd, text, text.Capacity);
            if (text.ToString() == title) { found = true; return false; }
            return true;
        }, IntPtr.Zero);

        return found;
    }
}
'@

# Physical pixels, so the rects the automation reports and the points the mouse
# moves to are in the same space.
[Win32]::SetProcessDpiAwarenessContext([IntPtr] (-4)) | Out-Null
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.Application]::EnableVisualStyles()

$log = 'C:\Temp\click-ribbon.txt'
function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }
"starting" | Out-File $log -Encoding utf8

Note ("screen {0} x {1}" -f [Win32]::GetSystemMetrics(0), [Win32]::GetSystemMetrics(1))

$inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')
$doc = $inv.ActiveDocument
Note "document: $($doc.DisplayName)"

# A cylindrical face, so the command does not have to ask for one.
$face = $null
foreach ($body in $doc.ComponentDefinition.SurfaceBodies) {
    foreach ($f in $body.Faces) {
        $radius = $null
        try { $radius = $f.Geometry.Radius } catch { continue }
        if ($null -ne $radius -and [Math]::Abs(($radius * 10) - 5.0) -lt 0.1) { $face = $f; break }
    }
    if ($null -ne $face) { break }
}

if ($null -eq $face) {
    Note "no 10 mm shaft in this document"
    exit 1
}

$doc.SelectSet.Clear()
$doc.SelectSet.Select($face)
Note "the shaft is selected"

$root = [System.Windows.Automation.AutomationElement]::RootElement

function FindByName([string] $name, [string] $type) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $name)
    foreach ($element in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)) {
        if ($null -eq $type -or $element.Current.ControlType.ProgrammaticName -eq $type) {
            return $element
        }
    }
    return $null
}

$dialogName = 'Create a Modelled Thread'

# The panel only shows while its tab is the one in front. Inventor's ribbon
# tabs come through as buttons, not tab items.
$tab = FindByName 'Modeled Threads' 'ControlType.Button'
if ($null -ne $tab) {
    try {
        $tab.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Note "the Modeled Threads tab was clicked"
    }
    catch {
        Note "the tab could not be clicked: $_"
    }

    Start-Sleep -Seconds 2
}
else {
    Note "no Modeled Threads tab on screen"
}

$button = FindByName 'Create Thread' 'ControlType.Button'
if ($null -eq $button) {
    Note "no Create Thread button on screen"
    exit 1
}

$rect = $button.Current.BoundingRectangle
Note ("button '{0}' at {1}" -f $button.Current.Name, $rect)

# Through the physical pixels first, then the logical ones if that misses.
foreach ($scale in 1.0) {
    $x = [int] ($rect.X + ($rect.Width / 2))
    $y = [int] ($rect.Y + ($rect.Height / 2))
    Note "clicking at $x, $y"
    [Win32]::Click($x, $y)

    # The dialog takes a moment: the command probes the face and works the plan
    # out before it shows.
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Seconds 1
        if ([Win32]::WindowExists($dialogName)) {
            Note "the dialog opened on a real click, after $($i + 1) s"
            exit 0
        }
    }

    Note "no dialog after the click at $x, $y"
}

Note "the button did not open the dialog"
exit 1
