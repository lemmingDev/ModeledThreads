# Reports the document tabs of every Inventor window, so the stray instance can
# be told from the one with the part.
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$log = 'C:\Temp\inventor-tabs.txt'
function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }
"starting" | Out-File $log -Encoding utf8

$root = [System.Windows.Automation.AutomationElement]::RootElement
$condition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ClassNameProperty, 'AfxMDIFrame140u')

foreach ($frame in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)) {
    Note ("window pid {0} at {1}" -f $frame.Current.ProcessId, $frame.Current.BoundingRectangle)

    $all = $frame.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($e in $all) {
        $type = $e.Current.ControlType.ProgrammaticName
        if ($type -eq 'ControlType.TabItem' -or $type -eq 'ControlType.Tab') {
            Note ("  {0} '{1}' selected={2}" -f $type, $e.Current.Name, $e.GetCurrentPropertyValue([System.Windows.Automation.SelectionItemPattern]::IsSelectedProperty))
        }
    }
}

Note "done"
