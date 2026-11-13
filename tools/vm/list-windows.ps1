# Lists the visible top-level windows, so a hidden or minimised Inventor or a
# dialog can be found by its real title.

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class Windows
{
    public delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    public static List<string> Listing = new List<string>();

    public static void Collect()
    {
        EnumWindows((hwnd, lParam) =>
        {
            if (!IsWindowVisible(hwnd)) { return true; }

            var text = new StringBuilder(512);
            GetWindowText(hwnd, text, text.Capacity);

            var cls = new StringBuilder(256);
            GetClassName(hwnd, cls, cls.Capacity);

            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);

            Listing.Add(hwnd + " pid=" + pid + " minimized=" + IsIconic(hwnd) + " | " + cls + " | " + text);
            return true;
        }, IntPtr.Zero);
    }
}
'@

$log = 'C:\Temp\windows.txt'
"starting" | Out-File $log -Encoding utf8
[Windows]::Collect()
foreach ($line in [Windows]::Listing) { $line | Out-File $log -Append -Encoding utf8 }
