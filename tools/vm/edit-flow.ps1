# Opens the Edit Thread dialog on the thread in the open part, the way a user
# would: select the thread in the browser, click its ribbon tab, click the
# button. Then it records what the dialog shows, so the pre-filled values can be
# checked without a human looking at the screen.

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

Add-Type -TypeDefinition @'
using System;
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

[Win32]::SetProcessDpiAwarenessContext([IntPtr] (-4)) | Out-Null

$log = 'C:\Temp\edit-flow.txt'
function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }
"starting" | Out-File $log -Encoding utf8

$inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')
$doc = $inv.ActiveDocument
Note "document: $($doc.DisplayName)"

# Select the thread's coil feature, which is what "Edit Thread" works from.
$feature = $null
foreach ($f in $doc.ComponentDefinition.Features) {
    if ($f.Name -like 'Modeled Thread*') { $feature = $f; break }
}

if ($null -eq $feature) {
    Note "no modelled thread in this document"
    exit 1
}

$doc.SelectSet.Clear()
$doc.SelectSet.Select($feature)
Note "selected feature: $($feature.Name)"

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

$dialogName = 'Edit a Modelled Thread'

$button = FindByName 'Edit Thread' 'ControlType.Button'
if ($null -eq $button) {
    Note "no Edit Thread button on screen"
    exit 1
}

$rect = $button.Current.BoundingRectangle
Note ("button '{0}' at {1}" -f $button.Current.Name, $rect)

$x = [int] ($rect.X + ($rect.Width / 2))
$y = [int] ($rect.Y + ($rect.Height / 2))
Note "clicking at $x, $y"
[Win32]::Click($x, $y)

for ($i = 0; $i -lt 20; $i++) {
    Start-Sleep -Seconds 1
    if ([Win32]::WindowExists($dialogName)) {
        Note "the edit dialog opened on a real click, after $($i + 1) s"
        break
    }
}

if (-not [Win32]::WindowExists($dialogName)) {
    Note "no edit dialog after the click"
    exit 1
}

Start-Sleep -Seconds 1

# What the dialog filled itself in with.
$dialog = FindByName $dialogName 'ControlType.Window'
if ($null -eq $dialog) {
    # The window may come through as a pane.
    $dialog = FindByName $dialogName $null
}

if ($null -ne $dialog) {
    $all = $dialog.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    Note "dialog elements: $($all.Count)"
    foreach ($e in $all) {
        $name = $e.Current.Name
        $type = $e.Current.ControlType.ProgrammaticName
        if ($type -eq 'ControlType.Edit' -or $type -eq 'ControlType.ComboBox' -or $type -eq 'ControlType.Text' -or $type -eq 'ControlType.Button' -or $type -eq 'ControlType.RadioButton' -or $type -eq 'ControlType.CheckBox') {
            $value = ''
            try {
                $pattern = $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
                $value = ' value="' + $pattern.Current.Value + '"'
            }
            catch { }
            Note ("{0} '{1}'{2}" -f $type, $name, $value)
        }
    }
}

# A picture of the dialog for the record.
try {
    $bounds = $dialog.Current.BoundingRectangle
    $width = [int] $bounds.Width
    $height = [int] $bounds.Height
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen([int] $bounds.X, [int] $bounds.Y, 0, 0, $bitmap.Size)
    $bitmap.Save('C:\Temp\edit-dialog.png', [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
    Note "saved C:\Temp\edit-dialog.png"
}
catch {
    Note "the picture failed: $_"
}

Note "done"
exit 0
