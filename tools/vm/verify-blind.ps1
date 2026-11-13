# Measures a blind hole after the dialog cut its thread: the bottom must still be
# a whole disk, the wall just above it must still be the drill size, and the
# mouth must be chamfered past the major diameter.

$log = 'C:\Temp\verify-blind.txt'
function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }
"starting" | Out-File $log -Encoding utf8

$inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')
$doc = $inv.ActiveDocument
$def = $doc.ComponentDefinition
Note "document: $($doc.DisplayName)"

$swept = 0
$faces = 0
$lowest = [double]::MaxValue
$highest = [double]::MinValue

foreach ($body in $def.SurfaceBodies) {
    foreach ($face in $body.Faces) {
        $faces++
        $isSwept = $face.SurfaceType -eq 12496   # kBSplineSurface
        if ($isSwept) { $swept++ }

        foreach ($edge in $face.Edges) {
            foreach ($vertex in @($edge.StartVertex, $edge.StopVertex)) {
                $p = $vertex.Point
                if ($isSwept) {
                    if ($p.Z * 10.0 -lt $lowest) { $lowest = $p.Z * 10.0 }
                    if ($p.Z * 10.0 -gt $highest) { $highest = $p.Z * 10.0 }
                }
            }
        }
    }
}

Note "faces: $faces, of them swept (thread): $swept"
Note ("the thread runs z {0:0.###} .. {1:0.###} mm" -f $lowest, $highest)

# The bottom of the hole: a flat disk at 5 mm.
$bottom = $null
foreach ($body in $def.SurfaceBodies) {
    foreach ($face in $body.Faces) {
        if ($face.SurfaceType -ne 33025) { continue }   # kPlaneSurface
        $z = $face.PointOnFace.Z * 10.0
        if ([Math]::Abs($z - 5.0) -lt 0.05) { $bottom = $face; break }
    }
    if ($null -ne $bottom) { break }
}

if ($null -eq $bottom) {
    Note "FAIL the bottom of the hole is gone"
}
else {
    $area = $bottom.Evaluator.Area * 100.0
    Note (("the bottom's area: {0:0.###} mm2 (a whole disk is {1:0.###})" -f $area, (([Math]::PI) * 4.25 * 4.25)))
}

# The wall just above the bottom, and the mouth.
$aboveBottom = 0.0
$atMouth = 0.0
foreach ($body in $def.SurfaceBodies) {
    foreach ($face in $body.Faces) {
        foreach ($edge in $face.Edges) {
            foreach ($vertex in @($edge.StartVertex, $edge.StopVertex)) {
                $p = $vertex.Point
                $r = [Math]::Sqrt(($p.X * $p.X) + ($p.Y * $p.Y)) * 10.0
                if ($r -gt 7.0) { continue }
                $z = $p.Z * 10.0
                if ($z -ge 4.95 -and $z -le 5.6 -and $r -gt $aboveBottom) { $aboveBottom = $r }
                if ($z -ge 19.8 -and $r -gt $atMouth) { $atMouth = $r }
            }
        }
    }
}

Note ("the wall just above the bottom: {0:0.###} mm from the axis (the drill is 4.25)" -f $aboveBottom)
Note ("the mouth: {0:0.###} mm from the axis (the major diameter is 5)" -f $atMouth)
Note "done"
