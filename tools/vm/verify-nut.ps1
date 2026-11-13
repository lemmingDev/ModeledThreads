# Measures the nut after the dialog cut its thread, so the numbers can be
# checked against the plan rather than only looked at.

$log = 'C:\Temp\verify-nut.txt'
function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }
"starting" | Out-File $log -Encoding utf8

$inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')
$doc = $inv.ActiveDocument
$def = $doc.ComponentDefinition
Note "document: $($doc.DisplayName)"

$widest = 0.0
$lowest = [double]::MaxValue
$highest = [double]::MinValue
$swept = 0
$faces = 0

foreach ($body in $def.SurfaceBodies) {
    foreach ($face in $body.Faces) {
        $faces++
        if ($face.SurfaceType -eq 12496) { $swept++ }   # kBSplineSurface

        foreach ($edge in $face.Edges) {
            foreach ($vertex in @($edge.StartVertex, $edge.StopVertex)) {
                $p = $vertex.Point
                $r = [Math]::Sqrt(($p.X * $p.X) + ($p.Y * $p.Y)) * 10.0
                if ($r -gt 7.0) { continue }
                if ($r -gt $widest) { $widest = $r }
                if ($p.Z * 10.0 -lt $lowest) { $lowest = $p.Z * 10.0 }
                if ($p.Z * 10.0 -gt $highest) { $highest = $p.Z * 10.0 }
            }
        }
    }
}

Note "faces: $faces, of them swept (thread): $swept"
Note ("the hole's widest point: {0:0.###} mm from the axis" -f $widest)
Note ("the thread runs z {0:0.###} .. {1:0.###} mm" -f $lowest, $highest)

# The mouth, a hair below the top face: a chamfered nut is wider there than the
# thread's major diameter.
$mouth = 0.0
foreach ($body in $def.SurfaceBodies) {
    foreach ($face in $body.Faces) {
        foreach ($edge in $face.Edges) {
            foreach ($vertex in @($edge.StartVertex, $edge.StopVertex)) {
                $p = $vertex.Point
                $r = [Math]::Sqrt(($p.X * $p.X) + ($p.Y * $p.Y)) * 10.0
                if ($r -gt 7.0) { continue }
                if ($p.Z * 10.0 -lt 7.9) { continue }
                if ($r -gt $mouth) { $mouth = $r }
            }
        }
    }
}

Note ("the mouth's widest point: {0:0.###} mm from the axis" -f $mouth)
Note "done"
