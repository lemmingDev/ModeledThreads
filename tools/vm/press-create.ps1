# Presses Create Thread in the open dialog and reports what the part became.

$log = 'C:\Temp\press-create.txt'
function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }
"starting" | Out-File $log -Encoding utf8

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')
$doc = $inv.ActiveDocument
Note "document: $($doc.DisplayName)"

function Volume([string] $label) {
    foreach ($body in $doc.ComponentDefinition.SurfaceBodies) {
        $v = $null
        try { $v = $body.Volume(1.0) } catch { try { $v = $body.Volume } catch { } }
        if ($null -ne $v) { Note "  $label body $($body.Name): $([Math]::Round($v * 1000.0, 3)) mm3" }
    }
}

Note "before:"
Volume 'before'

$root = [System.Windows.Automation.AutomationElement]::RootElement

function Find([string] $name) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $name)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

$dialog = Find 'Create a Modelled Thread'
if ($null -eq $dialog) { Note "the dialog is not open"; exit 1 }

$button = $null
$all = $dialog.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.Condition]::TrueCondition)
foreach ($element in $all) {
    $isButton = $element.Current.ControlType.ProgrammaticName -eq 'ControlType.Button'
    if ($element.Current.Name -eq 'Create Thread' -and $isButton) {
        $button = $element
        break
    }
}

if ($null -eq $button) {
    Note "no Create Thread button in the dialog; it holds:"
    foreach ($element in $all) {
        if ($element.Current.Name) { Note "  $($element.Current.ControlType.ProgrammaticName): $($element.Current.Name)" }
    }
    exit 1
}

# What the dialog says about the face and the size, before it is used.
Note "the dialog says:"
foreach ($element in $all) {
    $type = $element.Current.ControlType.ProgrammaticName
    $name = $element.Current.Name
    if ($name -and ($type -eq 'ControlType.Text' -or $type -eq 'ControlType.Edit')) {
        Note "  ${type}: $name"
    }
}

Note "pressing Create Thread"
$button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Seconds 12

$still = Find 'Create a Modelled Thread'
Note ($(if ($null -eq $still) { "the dialog closed" } else { "the dialog is still open" }))

$body = $doc.ComponentDefinition.SurfaceBodies.Item(1)
Note "features:"
foreach ($feature in $doc.ComponentDefinition.Features) {
    Note "  $($feature.Name) [$($feature.Type)]"
}

Note "after:"
Volume 'after'

try { Note "status bar: $($inv.StatusBarText)" } catch { Note "the status bar could not be read" }
Note "done"
