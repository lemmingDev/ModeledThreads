# Reports which Inventor process owns the window on screen, and whether that
# process has a document open.
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$log = 'C:\Temp\which-inventor.txt'
function Note([string] $text) { $text | Out-File $log -Append -Encoding utf8 }
"starting" | Out-File $log -Encoding utf8

$root = [System.Windows.Automation.AutomationElement]::RootElement
$condition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ClassNameProperty, 'AfxMDIFrame140u')

foreach ($frame in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)) {
    Note ("window '{0}' pid {1}" -f $frame.Current.Name, $frame.Current.ProcessId)
}

Note "done"
