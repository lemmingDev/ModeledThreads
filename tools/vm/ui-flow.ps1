# Selects the shaft's cylindrical face, then runs the add-in's Create Thread
# command, and reports whether the dialog came up. No mouse input is needed.

$ErrorActionPreference = 'Continue'
$log = 'C:\Temp\ui-flow.txt'
"starting" | Out-File $log -Encoding utf8

try {
    $inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')
}
catch {
    "Inventor could not be reached: $_" | Out-File $log -Append -Encoding utf8
    exit 1
}

$doc = $inv.ActiveDocument
"document: $($doc.DisplayName)" | Out-File $log -Append -Encoding utf8

# The shaft's cylindrical face: the one whose geometry has a 5 mm radius.
$face = $null
foreach ($body in $doc.ComponentDefinition.SurfaceBodies) {
    foreach ($f in $body.Faces) {
        $radius = $null
        try { $radius = $f.Geometry.Radius } catch { continue }
        if ($null -ne $radius -and [Math]::Abs(($radius * 10) - 5.0) -lt 0.1) {
            $face = $f
            break
        }
    }

    if ($null -ne $face) { break }
}

if ($null -eq $face) {
    "no cylindrical face was found" | Out-File $log -Append -Encoding utf8
    exit 1
}

"found the face" | Out-File $log -Append -Encoding utf8
$doc.SelectSet.Select($face)

# Run the command. Execute2 with false does not wait for the dialog.
$definition = $null
foreach ($d in $inv.CommandManager.ControlDefinitions) {
    if ($d.InternalName -eq 'ModeledThreads.Create') { $definition = $d; break }
}

if ($null -eq $definition) {
    "the command definition was not found" | Out-File $log -Append -Encoding utf8
    exit 1
}

"running the command" | Out-File $log -Append -Encoding utf8
$definition.Execute2($false)
Start-Sleep -Seconds 5

# Does the dialog exist?
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$condition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, 'Create a Modelled Thread')
$dialog = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
    [System.Windows.Automation.TreeScope]::Descendants, $condition)

if ($null -eq $dialog) {
    "the dialog did not appear" | Out-File $log -Append -Encoding utf8
}
else {
    "the dialog is up: $($dialog.Current.Name)" | Out-File $log -Append -Encoding utf8
}

"done" | Out-File $log -Append -Encoding utf8
