# Captures one Inventor window to a PNG, so the ribbon can be looked at without
# a screen recorder. Read-only: it does not click or type anything.
#
# Run in session 1:
#   powershell -ExecutionPolicy Bypass -File capture-window.ps1 <pid>

param(
    [Parameter(Mandatory = $true)][int] $InventorPid,
    [string] $OutFile = 'C:\Temp\inventor-window.png'
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class Shot
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hwnd, int command);
}
"@

[Shot]::SetProcessDPIAware() | Out-Null

$process = Get-Process -Id $InventorPid -ErrorAction Stop
$hwnd = $process.MainWindowHandle
if ($hwnd -eq [IntPtr]::Zero) {
    'no main window' | Out-File 'C:\Temp\capture.txt' -Encoding utf8
    exit 1
}

if ([Shot]::IsIconic($hwnd)) {
    [Shot]::ShowWindow($hwnd, 9) | Out-Null   # SW_RESTORE
    Start-Sleep -Milliseconds 800
}

$rect = New-Object Shot+RECT
if (-not [Shot]::GetWindowRect($hwnd, [ref] $rect)) {
    'GetWindowRect failed' | Out-File 'C:\Temp\capture.txt' -Encoding utf8
    exit 1
}

$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
"window {0},{1} {2}x{3}" -f $rect.Left, $rect.Top, $width, $height |
    Out-File 'C:\Temp\capture.txt' -Encoding utf8

$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
$graphics.Dispose()
$bitmap.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()

"saved $OutFile" | Out-File 'C:\Temp\capture.txt' -Append -Encoding utf8
