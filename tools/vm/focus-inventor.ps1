# Brings Inventor's main window to the front, and gets consoles out of the way.

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class Windows
{
    public delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    public const int Minimize = 6;
    public const int Restore = 9;

    public static IntPtr FindClass(string className)
    {
        IntPtr found = IntPtr.Zero;

        EnumWindows((hwnd, lParam) =>
        {
            if (!IsWindowVisible(hwnd)) { return true; }
            if (IsIconic(hwnd)) { return true; }

            var cls = new StringBuilder(256);
            GetClassName(hwnd, cls, cls.Capacity);
            if (cls.ToString() == className) { found = hwnd; return false; }
            return true;
        }, IntPtr.Zero);

        return found;
    }

    public static IntPtr FindClassEvenMinimised(string className)
    {
        IntPtr found = IntPtr.Zero;

        EnumWindows((hwnd, lParam) =>
        {
            var cls = new StringBuilder(256);
            GetClassName(hwnd, cls, cls.Capacity);
            if (cls.ToString() == className) { found = hwnd; return false; }
            return true;
        }, IntPtr.Zero);

        return found;
    }
}
'@

$log = 'C:\Temp\focus-inventor.txt'
"starting" | Out-File $log -Encoding utf8
function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }

# Consoles, this script's own included.
for ($i = 0; $i -lt 6; $i++) {
    $console = [Windows]::FindClass('ConsoleWindowClass')
    if ($console -eq [IntPtr]::Zero) { break }
    [Windows]::ShowWindow($console, [Windows]::Minimize) | Out-Null
    Start-Sleep -Milliseconds 200
}

$frame = [Windows]::FindClass('AfxMDIFrame140u')
if ($frame -eq [IntPtr]::Zero) {
    $frame = [Windows]::FindClassEvenMinimised('AfxMDIFrame140u')
    Note "Inventor is minimized; restoring it"
}

if ($frame -eq [IntPtr]::Zero) { Note "no Inventor window"; exit 1 }

Note "Inventor frame: $frame minimized=$([Windows]::IsIconic($frame))"
[Windows]::ShowWindow($frame, [Windows]::Restore) | Out-Null
[Windows]::BringWindowToTop($frame) | Out-Null
[Windows]::SetForegroundWindow($frame) | Out-Null

# Its size, so the caller knows where it ended up.
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class Place
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
}
'@

$rect = New-Object Place+Rect
[Place]::GetWindowRect($frame, [ref] $rect) | Out-Null
Note "Inventor rect: $($rect.Left),$($rect.Top) - $($rect.Right),$($rect.Bottom)"
