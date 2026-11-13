# Selects the shaft's face, then clicks the real "Create Thread" ribbon button
# through UI Automation, and reports whether the dialog came up.

$log = 'C:\Temp\ui-flow3.txt'
"starting" | Out-File $log -Encoding utf8

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

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

# Make sure the add-in's tab is the one on show.
$root = [System.Windows.Automation.AutomationElement]::RootElement

$tabCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, 'Modeled Threads')
$tab = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $tabCondition)
if ($null -ne $tab) {
    try {
        $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        "tab selected" | Out-File $log -Append -Encoding utf8
        Start-Sleep -Seconds 1
    }
    catch {
        "tab could not be selected: $_" | Out-File $log -Append -Encoding utf8
    }
}

$buttonCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, 'Create Thread')
$button = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $buttonCondition)

if ($null -eq $button) {
    "the button was not found" | Out-File $log -Append -Encoding utf8
    exit 1
}

"button found: $($button.Current.ControlType.ProgrammaticName)" | Out-File $log -Append -Encoding utf8

try {
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    "button invoked" | Out-File $log -Append -Encoding utf8
}
catch {
    "the button could not be invoked: $_" | Out-File $log -Append -Encoding utf8
    exit 1
}

Start-Sleep -Seconds 5

$dialogCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, 'Create a Modelled Thread')
$dialog = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $dialogCondition)

if ($null -eq $dialog) {
    "the dialog did not appear" | Out-File $log -Append -Encoding utf8
}
else {
    "the dialog is up" | Out-File $log -Append -Encoding utf8
}

"done" | Out-File $log -Append -Encoding utf8
