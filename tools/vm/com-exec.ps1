# Checks the Create Thread control's enabled state, then runs the command
# through COM to see whether the add-in's handler fires at all.

$log = 'C:\Temp\com-exec.txt'
"starting" | Out-File $log -Encoding utf8

function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }

$inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')
Note "document: $($inv.ActiveDocument.DisplayName)"

$definition = $null
foreach ($d in $inv.CommandManager.ControlDefinitions) {
    if ($d.InternalName -eq 'ModeledThreads.Create') { $definition = $d; break }
}

if ($null -eq $definition) { Note "no ModeledThreads.Create definition"; exit 1 }

Note "definition: '$($definition.DisplayName)' enabled=$($definition.Enabled) visible=$($definition.Visible) type=$($definition.CommandType)"

# Where the control sits, and whether it is enabled there.
foreach ($ribbon in $inv.UserInterfaceManager.Ribbons) {
    foreach ($tab in $ribbon.RibbonTabs) {
        if ($tab.InternalName -ne 'ModeledThreads.Tab') { continue }
        Note "ribbon '$($ribbon.InternalName)' has the tab (visible=$($tab.Visible))"

        foreach ($panel in $tab.RibbonPanels) {
            foreach ($control in $panel.CommandControls) {
                Note "  control '$($control.InternalName)' enabled=$($control.Enabled) visible=$($control.Visible) same=$([object]::ReferenceEquals($control.Definition, $definition))"
            }
        }
    }
}

# Select the shaft's face, so the command does not ask for one.
$doc = $inv.ActiveDocument
$face = $null
foreach ($body in $doc.ComponentDefinition.SurfaceBodies) {
    foreach ($f in $body.Faces) {
        $radius = $null
        try { $radius = $f.Geometry.Radius } catch { continue }
        if ($null -ne $radius -and [Math]::Abs(($radius * 10) - 5.0) -lt 0.1) { $face = $f; break }
    }
    if ($null -ne $face) { break }
}

if ($null -eq $face) { Note "no cylindrical face of radius 5 mm in this document" }
else { $doc.SelectSet.Select($face); Note "face selected" }

Note "executing the definition (this waits while the command runs)"
try {
    $definition.Execute2($true)
    Note "the definition returned"
}
catch {
    Note "the definition failed: $_"
}

Note "done"
