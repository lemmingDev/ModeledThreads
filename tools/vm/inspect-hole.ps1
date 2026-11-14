# Reads the part that is open and reports what the ends of its 15 mm hole look
# like: the planar faces there, the edges around them and the radii of those
# edges. Also lists the thread features, to see what the plugin built.

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
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);

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

$log = 'C:\Temp\inspect-hole.txt'
function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }
"starting" | Out-File $log -Encoding utf8

# A dialog left open would hold Inventor's main thread; close it first.
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

if ([Win32]::WindowExists('Edit a Modelled Thread')) {
    $close = FindByName 'Close' 'ControlType.Button'
    if ($null -ne $close) {
        $close.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Seconds 1
        Note "the edit dialog was closed"
    }
}

$inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')
Note ("caption: " + $inv.Caption)
Note ("documents: " + $inv.Documents.Count)
foreach ($d in $inv.Documents) { Note ("  doc '" + $d.DisplayName + "'  '" + $d.FullFileName + "'") }

$doc = $inv.ActiveDocument
if ($null -eq $doc) {
    foreach ($d in $inv.Documents) {
        if ($d.DisplayName -like 'thread*') { $d.Activate(); $doc = $d; break }
    }
}

if ($null -eq $doc) {
    Note "no document to inspect in this Inventor instance"
    Note "done"
    exit 0
}

Note "document: $($doc.DisplayName)  ($($doc.FullFileName))"

$def = $doc.ComponentDefinition

Note "features:"
foreach ($f in $def.Features) {
    Note ("  " + $f.Name)
}

function Project($point, $base, $axis) {
    return (($point.X - $base.X) * $axis.X) + (($point.Y - $base.Y) * $axis.Y) + (($point.Z - $base.Z) * $axis.Z)
}

$n = 0
foreach ($body in $def.SurfaceBodies) {
    foreach ($face in $body.Faces) {
        if ($face.SurfaceType -ne 5891) { continue }  # kCylinderSurface
        $cyl = $face.Geometry
        $radiusMm = $cyl.Radius * 10.0
        if ([Math]::Abs($radiusMm - 15.0) -gt 0.2) { continue }

        $n++
        Note ""
        Note ("cylinder {0}: radius {1:0.###} mm, axis {2:0.###},{3:0.###},{4:0.###}" -f $n, $radiusMm, $cyl.AxisVector.X, $cyl.AxisVector.Y, $cyl.AxisVector.Z)
        Note ("  base {0:0.###},{1:0.###},{2:0.###}  made by {3}" -f $cyl.BasePoint.X, $cyl.BasePoint.Y, $cyl.BasePoint.Z, $face.CreatedByFeature.Name)

        # The extent along the axis.
        $low = [double]::MaxValue
        $high = [double]::MinValue
        foreach ($edge in $face.Edges) {
            $t = Project $edge.PointOnEdge $cyl.BasePoint $cyl.AxisVector
            if ($t -lt $low) { $low = $t }
            if ($t -gt $high) { $high = $t }
        }

        Note ("  extent {0:0.###}..{1:0.###} mm" -f ($low * 10.0), ($high * 10.0))

        # The faces around each end.
        foreach ($end in @(@{ Name = 'low'; T = $low }, @{ Name = 'high'; T = $high })) {
            Note ("  end {0} at {1:0.###} mm:" -f $end.Name, ($end.T * 10.0))
            foreach ($edge in $face.Edges) {
                $t = Project $edge.PointOnEdge $cyl.BasePoint $cyl.AxisVector
                if ([Math]::Abs($t - $end.T) -gt 0.02) { continue }

                $edgeKind = '?'
                $edgeRadius = 0
                try {
                    $g = $edge.Geometry
                    $edgeKind = $g.GetType().Name
                    try { $edgeRadius = $g.Radius * 10.0 } catch { }
                }
                catch { }

                Note ("    edge {0} radius {1:0.###} mm" -f $edgeKind, $edgeRadius)

                foreach ($other in $edge.Faces) {
                    if ($other.TransientKey -eq $face.TransientKey) { continue }

                    $planeInfo = ''
                    if ($other.SurfaceType -eq 5890) {  # kPlaneSurface
                        $pl = $other.Geometry
                        $planeInfo = ("plane at {0:0.###} mm" -f (Project $pl.RootPoint $cyl.BasePoint $cyl.AxisVector) * 10.0)
                    }

                    Note ("      neighbour {0} {1}" -f $other.SurfaceType, $planeInfo)

                    # Every edge of the neighbour, with its radius: a smaller
                    # circle means a hole carries on past this face.
                    foreach ($neighbourEdge in $other.Edges) {
                        $kind = '?'
                        $radius = 0
                        try {
                            $g = $neighbourEdge.Geometry
                            $kind = $g.GetType().Name
                            try { $radius = $g.Radius * 10.0 } catch { }
                        }
                        catch { }

                        Note ("        its edge {0} radius {1:0.###} mm" -f $kind, $radius)
                    }
                }
            }
        }
    }
}

Note ""
if ($n -eq 0) { Note "no 15 mm cylindrical face in this document" }

Note "done"
exit 0
