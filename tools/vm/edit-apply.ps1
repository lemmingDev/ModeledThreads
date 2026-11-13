# Changes a modelled thread through its dialog: opens the Edit Thread dialog on
# the thread in the part, changes the size in the dialog, presses Apply, and
# reports what the model looks like afterwards. Ends by dumping what UI
# Automation sees of the browser, so the double-click route can be tried next.

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

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

$log = 'C:\Temp\edit-apply.txt'
function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }
"starting" | Out-File $log -Encoding utf8

$inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')
$doc = $inv.ActiveDocument
Note "document: $($doc.DisplayName)"

function Get-Volume {
    return [Math]::Round($doc.ComponentDefinition.MassProperties.Volume * 1000.0, 1)
}

function Get-ThreadFeatures {
    return @($doc.ComponentDefinition.Features | Where-Object { $_.Name -like 'Modeled Thread*' })
}

function Get-Record([object] $feature) {
    try {
        $sets = $feature.AttributeSets
        $set = $sets.Item('ModeledThreads')
        return $set.Item('Thread').Value
    }
    catch {
        return "(no record: $_)"
    }
}

$before = Get-Volume
$beforeFeatures = Get-ThreadFeatures
Note ("before: volume {0} mm3, {1} thread features" -f $before, $beforeFeatures.Count)
foreach ($f in $beforeFeatures) { Note ("  {0}: {1}" -f $f.Name, (Get-Record $f)) }

# ---- open the dialog ------------------------------------------------
$feature = $beforeFeatures[0]
$doc.SelectSet.Clear()
$doc.SelectSet.Select($feature)

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
    $tab.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Seconds 2
}

$button = FindByName 'Edit Thread' 'ControlType.Button'
if ($null -eq $button) {
    Note "no Edit Thread button on screen"
    exit 1
}

$rect = $button.Current.BoundingRectangle
[Win32]::Click([int] ($rect.X + ($rect.Width / 2)), [int] ($rect.Y + ($rect.Height / 2)))

$dialogName = 'Edit a Modelled Thread'
for ($i = 0; $i -lt 20; $i++) {
    Start-Sleep -Seconds 1
    if ([Win32]::WindowExists($dialogName)) { break }
}

if (-not [Win32]::WindowExists($dialogName)) {
    Note "no edit dialog"
    exit 1
}

Note "the edit dialog is open"
Start-Sleep -Seconds 1

# ---- change the size in the dialog ----------------------------------
$dialog = FindByName $dialogName 'ControlType.Window'
$designation = $null
$all = $dialog.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.Condition]::TrueCondition)
foreach ($e in $all) {
    if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.Edit' -and $e.Current.Name -eq 'Designation') {
        $designation = $e
        break
    }
}

if ($null -eq $designation) {
    Note "no Designation field in the dialog"
    exit 1
}

$designation.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('M10x1')
Note "the designation was typed as M10x1"

# ---- apply ----------------------------------------------------------
$apply = $null
foreach ($e in $dialog.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.Condition]::TrueCondition)) {
    if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.Button' -and $e.Current.Name -eq 'Apply') {
        $apply = $e
        break
    }
}

if ($null -eq $apply) {
    Note "no Apply button in the dialog"
    exit 1
}

$apply.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Note "Apply was clicked"

for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Seconds 1
    if (-not [Win32]::WindowExists($dialogName)) { break }
}

if ([Win32]::WindowExists($dialogName)) {
    # A warning may be showing on top of the dialog; note it and close it.
    Note "the dialog is still open after Apply"
    $warning = FindByName 'Modeled Threads' 'ControlType.Window'
    if ($null -ne $warning) {
        Note "a message box is up: $($warning.Current.Name)"
    }
    exit 1
}

Note "the dialog closed after Apply"
Start-Sleep -Seconds 2

# ---- what the model looks like now ----------------------------------
$after = Get-Volume
$afterFeatures = Get-ThreadFeatures
Note ("after: volume {0} mm3, {1} thread features" -f $after, $afterFeatures.Count)
foreach ($f in $afterFeatures) { Note ("  {0}: {1}" -f $f.Name, (Get-Record $f)) }

Note ("volume changed by {0} mm3" -f [Math]::Round($after - $before, 1))

# ---- what the browser shows -----------------------------------------
$frameCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ClassNameProperty, 'AfxMDIFrame140u')
$frame = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $frameCondition)
if ($null -ne $frame) {
    $items = $frame.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($e in $items) {
        $name = $e.Current.Name
        if ($name -like '*Modeled Thread*' -or $e.Current.ControlType.ProgrammaticName -eq 'ControlType.TreeItem') {
            Note ("browser: {0} '{1}' at {2}" -f $e.Current.ControlType.ProgrammaticName, $name, $e.Current.BoundingRectangle)
        }
    }
}

Note "done"
exit 0
