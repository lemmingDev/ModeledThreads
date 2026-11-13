# Selects a cylindrical face of a given radius and runs the Create Thread
# command, so the dialog opens on it. Nothing is cut: the dialog is left for the
# next step to press.

param(
    [double] $Radius = 4.25
)

$log = 'C:\Temp\com-exec-face.txt'
function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }
"starting, looking for a {0:0.###} mm face" -f $Radius | Out-File $log -Encoding utf8

$inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')
$doc = $inv.ActiveDocument
Note "document: $($doc.DisplayName)"

$definition = $null
foreach ($d in $inv.CommandManager.ControlDefinitions) {
    if ($d.InternalName -eq 'ModeledThreads.Create') { $definition = $d; break }
}

if ($null -eq $definition) { Note "no ModeledThreads.Create definition"; exit 1 }
Note "definition: '$($definition.DisplayName)' enabled=$($definition.Enabled)"

$face = $null
foreach ($body in $doc.ComponentDefinition.SurfaceBodies) {
    foreach ($f in $body.Faces) {
        $radius = $null
        try { $radius = $f.Geometry.Radius } catch { continue }
        if ($null -ne $radius -and [Math]::Abs(($radius * 10) - $Radius) -lt 0.1) { $face = $f; break }
    }
    if ($null -ne $face) { break }
}

if ($null -eq $face) {
    Note "no face of that radius in this document"
    exit 1
}

$doc.SelectSet.Clear()
$doc.SelectSet.Select($face)
Note "the face is selected"

Note "executing the definition (this waits while the dialog runs)"
try {
    $definition.Execute2($true)
    Note "the definition returned"
}
catch {
    Note "the definition failed: $_"
}

Note "done"
