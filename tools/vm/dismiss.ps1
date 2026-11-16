# Clicks the OK button of the add-in's message box. The window is found by
# walking the top-level windows, because FindWindow on this desktop does not
# match it by title.
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class Box2
{
    public delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    public static IntPtr Dialog;
    public static string Seen = string.Empty;

    public static void Find(string title)
    {
        EnumWindows((hwnd, lParam) =>
        {
            var text = new StringBuilder(512);
            GetWindowText(hwnd, text, text.Capacity);
            if (text.ToString() == title)
            {
                Dialog = hwnd;
                Seen = text.ToString();
                return false;
            }

            return true;
        }, IntPtr.Zero);
    }
}
'@

$out = 'C:\Temp\dismiss.txt'
"pressing" | Out-File $out -Encoding utf8

[Box2]::Find('Modeled Threads')
"dialog: $([Box2]::Dialog) '$([Box2]::Seen)'" | Out-File $out -Append -Encoding utf8

if ([Box2]::Dialog -ne [IntPtr]::Zero) {
    $button = [Box2]::FindWindowEx([Box2]::Dialog, [IntPtr]::Zero, 'Button', 'OK')
    "button: $button" | Out-File $out -Append -Encoding utf8

    if ($button -ne [IntPtr]::Zero) {
        [Box2]::SendMessage($button, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
        "pressed" | Out-File $out -Append -Encoding utf8
    }
}
