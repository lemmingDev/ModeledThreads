# Closes the add-in's edit dialog, if one is open, so the part can be worked on
# without a modal window in the way. Targets the Inventor process given in
# C:\Temp\close-pid.txt (or any, if the file is missing).
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$log = 'C:\Temp\close-dialog.txt'
"starting" | Out-File $log -Encoding utf8

$pidToClose = 0
if (Test-Path 'C:\Temp\close-pid.txt') {
    $text = (Get-Content 'C:\Temp\close-pid.txt' -Raw).Trim()
    [int]::TryParse($text, [ref] $pidToClose) | Out-Null
}

$root = [System.Windows.Automation.AutomationElement]::RootElement
$condition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, 'Edit a Modelled Thread')

$dialog = $null
foreach ($element in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)) {
    if ($pidToClose -ne 0 -and $element.Current.ProcessId -ne $pidToClose) { continue }
    $dialog = $element
    break
}

if ($null -eq $dialog) {
    "no edit dialog for pid $pidToClose" | Out-File $log -Append -Encoding utf8
    exit 0
}

"dialog in pid $($dialog.Current.ProcessId)" | Out-File $log -Append -Encoding utf8

$all = $dialog.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.Condition]::TrueCondition)
foreach ($e in $all) {
    if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.Button' -and $e.Current.Name -eq 'Close') {
        $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        "Close was clicked" | Out-File $log -Append -Encoding utf8
        break
    }
}

Start-Sleep -Seconds 1
"done" | Out-File $log -Append -Encoding utf8
exit 0
