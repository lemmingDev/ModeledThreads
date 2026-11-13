# Leaves the demo bolt open and clean: no work planes, axes or sketches, fitted
# to the window, so a screenshot shows only the part.

$log = 'C:\Temp\clean-view.txt'
function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }
"starting" | Out-File $log -Encoding utf8

$inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')

# The threaded demo part, if it is not the one on show.
$doc = $null
foreach ($d in $inv.Documents) {
    if ($d.DisplayName -eq 'demo-bolt.ipt') { $doc = $d; break }
}

if ($null -eq $doc) {
    $doc = $inv.Documents.Open('C:\Temp\demo-bolt.ipt', $true)
    Note "opened $($doc.DisplayName)"
}

$doc.Activate()
$def = $doc.ComponentDefinition

foreach ($plane in $def.WorkPlanes) { $plane.Visible = $false }
foreach ($axis in $def.WorkAxes) { $axis.Visible = $false }
foreach ($point in $def.WorkPoints) { $point.Visible = $false }
foreach ($sketch in $def.Sketches) { $sketch.Visible = $false }

Note "annotations hidden"

# Look at the thread from the side, a little above.
$view = $inv.ActiveView
$geometry = $inv.TransientGeometry
$camera = $view.Camera

$camera.Eye = $geometry.CreatePoint(20, -30, 26)
$camera.Target = $geometry.CreatePoint(0, 0, 14)
$camera.UpVector = $geometry.CreateUnitVector(0, 0, 1)
$camera.ViewOrientationType = 10759

$view.Fit()
Note "the view is set"
