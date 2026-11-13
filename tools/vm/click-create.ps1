# Clicks the Create Thread button, then the shaft in the view, so the dialog
# opens. Used to check the user-facing flow end to end.

Add-Type -AssemblyName System.Windows.Forms

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class Win32
{
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
    public const uint LeftDown = 0x0002;
    public const uint LeftUp = 0x0004;
}
'@

function Click([int] $x, [int] $y) {
    [Win32]::SetCursorPos($x, $y) | Out-Null
    Start-Sleep -Milliseconds 250
    [Win32]::mouse_event([Win32]::LeftDown, 0, 0, 0, 0)
    [Win32]::mouse_event([Win32]::LeftUp, 0, 0, 0, 0)
    Start-Sleep -Milliseconds 500
}

# The Create Thread button on the Modeled Threads tab. The screen is 2940 wide,
# which is 1.55 times the width the screenshots are shown at.
Click 62 155
Start-Sleep -Seconds 3

# The threaded part of the shaft in the graphics window.
Click 1733 743
Start-Sleep -Seconds 4

"clicked" | Out-File 'C:\Temp\ui-click.txt' -Encoding utf8
