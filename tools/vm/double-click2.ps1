# Finds out when Inventor raises UserInputEvents.OnDoubleClick at all: first on
# the browser row of a modelled thread, then on the model in the graphics
# window. Each attempt is followed by a look at the add-in's log.

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
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    public const uint LeftDown = 0x0002;
    public const uint LeftUp = 0x0004;

    public static void Click(int x, int y)
    {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(200);
        mouse_event(LeftDown, 0, 0, 0, 0);
        mouse_event(LeftUp, 0, 0, 0, 0);
    }

    public static void DoubleClick(int x, int y)
    {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(300);
        for (int i = 0; i < 2; i++)
        {
            mouse_event(LeftDown, 0, 0, 0, 0);
            mouse_event(LeftUp, 0, 0, 0, 0);
            System.Threading.Thread.Sleep(90);
        }
    }

    public static IntPtr FindWindow(string title)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hwnd, lParam) =>
        {
            var text = new StringBuilder(512);
            GetWindowText(hwnd, text, text.Capacity);
            if (text.ToString() == title) { found = hwnd; return false; }
            return true;
        }, IntPtr.Zero);

        return found;
    }

    public static bool WindowExists(string title)
    {
        return FindWindow(title) != IntPtr.Zero;
    }
}
'@

[Win32]::SetProcessDpiAwarenessContext([IntPtr] (-4)) | Out-Null

$log = 'C:\Temp\double-click2.txt'
function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }
"starting" | Out-File $log -Encoding utf8

$inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')
$doc = $inv.ActiveDocument
Note "document: $($doc.DisplayName)"

$logFile = 'C:\Users\jwill872\AppData\Local\ModeledThreads\modeled-threads.log'
function LogTail([int] $count) {
    if (Test-Path $logFile) { Get-Content $logFile -Tail $count | ForEach-Object { Note ("  log: " + $_) } }
}

$root = [System.Windows.Automation.AutomationElement]::RootElement
$frameCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ClassNameProperty, 'AfxMDIFrame140u')
$frame = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $frameCondition)

if ($null -eq $frame) {
    Note "no Inventor window"
    exit 1
}

$handle = [Win32]::FindWindow('Autodesk Inventor Professional 2027 - EDUCATION (NON COMMERCIAL)')
if ($handle -ne [IntPtr]::Zero) {
    [Win32]::SetForegroundWindow($handle) | Out-Null
    Start-Sleep -Milliseconds 500
    Note "Inventor brought to the front"
}

$inv.ActiveView.Fit()
Start-Sleep -Milliseconds 500

function SelectionCount {
    try { return $doc.SelectSet.Count } catch { return -1 }
}

# ---- 1: the browser row --------------------------------------------
$item = $null
foreach ($e in $frame.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.Condition]::TrueCondition)) {
    if ($e.Current.ControlType.ProgrammaticName -ne 'ControlType.ListItem') { continue }
    $name = $e.Current.Name
    if ($name -like '*Modeled Thread M10*' -and $name -notlike '*Lead-in*' -and $name -notlike '*Relief*') {
        $item = $e
        break
    }
}

$doc.SelectSet.Clear()
Start-Sleep -Milliseconds 300
Note ("browser: selection is {0} before" -f (SelectionCount))

if ($null -ne $item) {
    $rect = $item.Current.BoundingRectangle
    $x = [int] ($rect.X + 150)
    $y = [int] ($rect.Y + ($rect.Height / 2))
    Note ("browser row '{0}' at {1}, clicking at {2},{3}" -f $item.Current.Name, $rect, $x, $y)
    [Win32]::Click($x, $y)
    Start-Sleep -Milliseconds 600
    Note ("browser: selection is {0} after one click" -f (SelectionCount))

    [Win32]::DoubleClick($x, $y)
    Start-Sleep -Seconds 2
    Note ("browser: selection is {0} after the double-click" -f (SelectionCount))
    Note ("browser: edit dialog open = {0}" -f [Win32]::WindowExists('Edit a Modelled Thread'))
    LogTail 4
    $doc.SelectSet.Clear()
}
else {
    Note "no browser row"
}

# ---- 2: the model in the graphics window ---------------------------
$bounds = $frame.Current.BoundingRectangle
$gx = [int] ($bounds.X + ($bounds.Width * 0.62))
$gy = [int] ($bounds.Y + ($bounds.Height * 0.55))
Note "graphics double-click at $gx, $gy"
[Win32]::DoubleClick($gx, $gy)
Start-Sleep -Seconds 3
Note ("graphics: selection is {0} after the double-click" -f (SelectionCount))
Note ("graphics: edit dialog open = {0}" -f [Win32]::WindowExists('Edit a Modelled Thread'))
LogTail 6

Note "done"
exit 0
