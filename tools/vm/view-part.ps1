# Opens a part, hides everything but the solid, and points the camera at it, so
# a screenshot shows the model and nothing else.

param(
    [string] $File = 'C:\Temp\demo-bolt.ipt',
    [double] $EyeX = 20,
    [double] $EyeY = -30,
    [double] $EyeZ = 26,
    [double] $TargetX = 0,
    [double] $TargetY = 0,
    [double] $TargetZ = 14
)

$log = 'C:\Temp\view-part.txt'
function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }
"starting" | Out-File $log -Encoding utf8

$inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')

$name = Split-Path $File -Leaf
$doc = $null
foreach ($d in $inv.Documents) {
    if ($d.DisplayName -eq $name) { $doc = $d; break }
}

if ($null -eq $doc) {
    $doc = $inv.Documents.Open($File, $true)
    Note "opened $($doc.DisplayName)"
}

$doc.Activate()
$def = $doc.ComponentDefinition

foreach ($plane in $def.WorkPlanes) { $plane.Visible = $false }
foreach ($axis in $def.WorkAxes) { $axis.Visible = $false }
foreach ($point in $def.WorkPoints) { $point.Visible = $false }
foreach ($sketch in $def.Sketches) { $sketch.Visible = $false }

Note "annotations hidden"

$view = $inv.ActiveView
$geometry = $inv.TransientGeometry
$camera = $view.Camera

$camera.Eye = $geometry.CreatePoint($EyeX, $EyeY, $EyeZ)
$camera.Target = $geometry.CreatePoint($TargetX, $TargetY, $TargetZ)
$camera.UpVector = $geometry.CreateUnitVector(0, 0, 1)

$view.Fit()

Note "the view is set"
