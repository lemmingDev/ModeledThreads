# Selects the shaft's cylindrical face and runs Create Thread. Execute() waits
# for the command, so this script stays inside it while the dialog is up.

$log = 'C:\Temp\ui-flow2.txt'
"starting" | Out-File $log -Encoding utf8

$inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')
$doc = $inv.ActiveDocument
"document: $($doc.DisplayName)" | Out-File $log -Append -Encoding utf8

$face = $null
foreach ($body in $doc.ComponentDefinition.SurfaceBodies) {
    foreach ($f in $body.Faces) {
        $radius = $null
        try { $radius = $f.Geometry.Radius } catch { continue }
        if ($null -ne $radius -and [Math]::Abs(($radius * 10) - 5.0) -lt 0.1) { $face = $f; break }
    }

    if ($null -ne $face) { break }
}

if ($null -eq $face) {
    "no cylindrical face" | Out-File $log -Append -Encoding utf8
    exit 1
}

$doc.SelectSet.Select($face)
"face selected" | Out-File $log -Append -Encoding utf8

$definition = $null
foreach ($d in $inv.CommandManager.ControlDefinitions) {
    if ($d.InternalName -eq 'ModeledThreads.Create') { $definition = $d; break }
}

if ($null -eq $definition) {
    "no command definition" | Out-File $log -Append -Encoding utf8
    exit 1
}

"running the command (this waits for it)" | Out-File $log -Append -Encoding utf8
$definition.Execute()
"the command returned" | Out-File $log -Append -Encoding utf8
