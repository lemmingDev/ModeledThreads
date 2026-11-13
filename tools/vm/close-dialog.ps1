# Closes the thread dialog without cutting anything, so a test can start from a
# clean part.

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$log = 'C:\Temp\close-dialog.txt'
function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }
"starting" | Out-File $log -Encoding utf8

$root = [System.Windows.Automation.AutomationElement]::RootElement
$condition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, 'Create a Modelled Thread')
$dialog = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)

if ($null -eq $dialog) {
    Note "the dialog is not open"
    exit 0
}

$all = $dialog.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.Condition]::TrueCondition)

foreach ($element in $all) {
    $isButton = $element.Current.ControlType.ProgrammaticName -eq 'ControlType.Button'
    if ($isButton -and $element.Current.Name -eq 'Close') {
        $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Note "the dialog was closed"
        exit 0
    }
}

Note "the dialog has no Close button; it holds:"
foreach ($element in $all) {
    if ($element.Current.Name) { Note "  $($element.Current.ControlType.ProgrammaticName): $($element.Current.Name)" }
}
