# Brings the thread dialog to the front and gets console windows out of the way,
# so a screenshot shows the dialog and not the task's own console.

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
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hwnd);

    public const int Minimize = 6;

    public static List<string> Listing = new List<string>();

    public static IntPtr Find(string title, string className)
    {
        IntPtr found = IntPtr.Zero;

        EnumWindows((hwnd, lParam) =>
        {
            if (!IsWindowVisible(hwnd)) { return true; }

            var text = new StringBuilder(512);
            GetWindowText(hwnd, text, text.Capacity);

            var cls = new StringBuilder(256);
            GetClassName(hwnd, cls, cls.Capacity);

            if (Listing.Count < 40) { Listing.Add(cls + " | " + text); }

            if (title != null && text.ToString() == title) { found = hwnd; return false; }
            if (className != null && cls.ToString() == className) { found = hwnd; return false; }

            return true;
        }, IntPtr.Zero);

        return found;
    }
}
'@

$log = 'C:\Temp\show-dialog.txt'
function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }
"starting" | Out-File $log -Encoding utf8

# Out of the way: every console window, this script's own included.
for ($i = 0; $i -lt 6; $i++) {
    $console = [Windows]::Find($null, 'ConsoleWindowClass')
    if ($console -eq [IntPtr]::Zero) { break }
    [Windows]::ShowWindow($console, [Windows]::Minimize) | Out-Null
    Start-Sleep -Milliseconds 200
}

$dialog = [Windows]::Find('Create a Modelled Thread', $null)
Note "dialog handle: $dialog"

if ($dialog -eq [IntPtr]::Zero) {
    [Windows]::Find($null, $null) | Out-Null
    foreach ($line in [Windows]::Listing) { Note "  $line" }
    exit 1
}

[Windows]::ShowWindow($dialog, 9) | Out-Null   # SW_RESTORE
[Windows]::BringWindowToTop($dialog) | Out-Null
[Windows]::SetForegroundWindow($dialog) | Out-Null

Note "the dialog is in front"
