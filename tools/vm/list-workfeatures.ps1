# Lists a part's work planes, axes, points and sketches with their visibility,
# to check that a thread leaves nothing behind on screen.

$log = 'C:\Temp\workfeatures.txt'
function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }
"starting" | Out-File $log -Encoding utf8

$inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')
$doc = $inv.ActiveDocument
$def = $doc.ComponentDefinition
Note "document: $($doc.DisplayName)"

$visiblePlanes = 0
Note "work planes:"
foreach ($plane in $def.WorkPlanes) {
    $visible = $plane.Visible
    if ($visible) { $visiblePlanes++ }
    Note ("  {0,-34} visible={1}" -f $plane.Name, $visible)
}

$visibleAxes = 0
Note "work axes:"
foreach ($axis in $def.WorkAxes) {
    $visible = $axis.Visible
    if ($visible) { $visibleAxes++ }
    Note ("  {0,-34} visible={1}" -f $axis.Name, $visible)
}

$visiblePoints = 0
Note "work points:"
foreach ($point in $def.WorkPoints) {
    $visible = $point.Visible
    if ($visible) { $visiblePoints++ }
    Note ("  {0,-34} visible={1}" -f $point.Name, $visible)
}

$visibleSketches = 0
Note "sketches:"
foreach ($sketch in $def.Sketches) {
    $visible = $sketch.Visible
    if ($visible) { $visibleSketches++ }
    Note ("  {0,-34} visible={1}" -f $sketch.Name, $visible)
}

Note "visible planes: $visiblePlanes, axes: $visibleAxes, points: $visiblePoints, sketches: $visibleSketches"
Note "done"
