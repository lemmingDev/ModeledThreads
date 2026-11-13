# Looks at how the Create Thread definition arrives as a .NET object: its type,
# and whether the event that should run the command is on it at all.

$log = 'C:\Temp\com-type.txt'
"starting" | Out-File $log -Encoding utf8
function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }

[void] [Reflection.Assembly]::LoadFile('C:\Temp\ThreadHarness\Autodesk.Inventor.Interop.dll')

$inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')
$definition = $null
foreach ($d in $inv.CommandManager.ControlDefinitions) {
    if ($d.InternalName -eq 'ModeledThreads.Create') { $definition = $d; break }
}

if ($null -eq $definition) { Note "no definition"; exit 1 }

Note "runtime type: $($definition.GetType().FullName)"
Note "is ButtonDefinitionClass: $($definition -is [Inventor.ButtonDefinitionClass])"
Note "is ButtonDefinition interface: $($definition -is [Inventor.ButtonDefinition])"

Note "events on the runtime type:"
foreach ($e in $definition.GetType().GetEvents()) { Note "  event: $($e.Name) ($($e.EventHandlerType.FullName))" }

Note "events on the interface:"
foreach ($e in [Inventor.ButtonDefinition].GetEvents()) { Note "  event: $($e.Name) ($($e.EventHandlerType.FullName))" }

$panel = $null
foreach ($ribbon in $inv.UserInterfaceManager.Ribbons) {
    if ($ribbon.InternalName -ne 'Part') { continue }
    foreach ($tab in $ribbon.RibbonTabs) {
        if ($tab.InternalName -ne 'ModeledThreads.Tab') { continue }
        foreach ($p in $tab.RibbonPanels) { $panel = $p }
    }
}

if ($null -eq $panel) { Note "no panel"; exit 1 }

foreach ($control in $panel.CommandControls) {
    $d = $control.Definition
    Note "control '$($control.InternalName)' definition type: $($d.GetType().FullName)"
    Note "  is interface: $($d -is [Inventor.ButtonDefinition])"
}

Note "done"
