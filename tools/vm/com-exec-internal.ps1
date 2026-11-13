# Selects the nut's hole and runs the Create Thread command, so the dialog
# opens on an internal face. Nothing is cut: the dialog is left for the next
# step to press.

$log = 'C:\Temp\com-exec-internal.txt'
function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }
"starting" | Out-File $log -Encoding utf8

$inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')
$doc = $inv.ActiveDocument
Note "document: $($doc.DisplayName)"

$definition = $null
foreach ($d in $inv.CommandManager.ControlDefinitions) {
    if ($d.InternalName -eq 'ModeledThreads.Create') { $definition = $d; break }
}

if ($null -eq $definition) { Note "no ModeledThreads.Create definition"; exit 1 }
Note "definition: '$($definition.DisplayName)' enabled=$($definition.Enabled)"

# The hole's face: the cylinder of the tap drill, 4.25 mm.
$face = $null
foreach ($body in $doc.ComponentDefinition.SurfaceBodies) {
    foreach ($f in $body.Faces) {
        $radius = $null
        try { $radius = $f.Geometry.Radius } catch { continue }
        if ($null -ne $radius -and [Math]::Abs(($radius * 10) - 4.25) -lt 0.1) { $face = $f; break }
    }
    if ($null -ne $face) { break }
}

if ($null -eq $face) {
    Note "no 8.5 mm hole in this document"
    exit 1
}

$doc.SelectSet.Clear()
$doc.SelectSet.Select($face)
Note "the hole is selected"

Note "executing the definition (this waits while the dialog runs)"
try {
    $definition.Execute2($true)
    Note "the definition returned"
}
catch {
    Note "the definition failed: $_"
}

Note "done"
