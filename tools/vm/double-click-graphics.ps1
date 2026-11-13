# Double-clicks the thread on the model itself, in the graphics window, and
# reports whether the add-in's dialog opens.

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

    public static bool WindowExists(string title) { return FindWindow(title) != IntPtr.Zero; }
}
'@

[Win32]::SetProcessDpiAwarenessContext([IntPtr] (-4)) | Out-Null

$log = 'C:\Temp\double-click-graphics.txt'
function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }
"starting" | Out-File $log -Encoding utf8

$inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')
$doc = $inv.ActiveDocument
Note "document: $($doc.DisplayName)"

$handle = [Win32]::FindWindow('Autodesk Inventor Professional 2027 - EDUCATION (NON COMMERCIAL)')
if ($handle -ne [IntPtr]::Zero) {
    [Win32]::SetForegroundWindow($handle) | Out-Null
    Start-Sleep -Milliseconds 500
}

$inv.ActiveView.Fit()
Start-Sleep -Milliseconds 500

$doc.SelectSet.Clear()
Start-Sleep -Milliseconds 300

$logFile = 'C:\Users\jwill872\AppData\Local\ModeledThreads\modeled-threads.log'
$before = if (Test-Path $logFile) { (Get-Content $logFile).Count } else { 0 }

# A point on the shaft, away from the browser: the thread's surface.
$root = [System.Windows.Automation.AutomationElement]::RootElement
$frameCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ClassNameProperty, 'AfxMDIFrame140u')
$frame = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $frameCondition)
$bounds = $frame.Current.BoundingRectangle

$x = [int] ($bounds.X + ($bounds.Width * 0.62))
$y = [int] ($bounds.Y + ($bounds.Height * 0.55))
Note "double-click at $x, $y"
[Win32]::DoubleClick($x, $y)

$opened = $false
for ($i = 0; $i -lt 12; $i++) {
    Start-Sleep -Seconds 1
    if ([Win32]::WindowExists('Edit a Modelled Thread')) { $opened = $true; break }
}

Note ("the add-in's edit dialog opened = {0}" -f $opened)

if (Test-Path $logFile) {
    Get-Content $logFile | Select-Object -Skip $before | ForEach-Object { Note ("  log: " + $_) }
}

Note "done"
exit 0
