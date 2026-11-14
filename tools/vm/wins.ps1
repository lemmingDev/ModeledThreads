# Lists top-level windows, so a modal dialog blocking Inventor can be spotted.
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$out = 'C:\Temp\wins.txt'
"windows" | Out-File $out -Encoding utf8

$root = [System.Windows.Automation.AutomationElement]::RootElement
$all = $root.FindAll([System.Windows.Automation.TreeScope]::Children,
    [System.Windows.Automation.Condition]::TrueCondition)

foreach ($w in $all) {
    $name = $w.Current.Name
    $pid2 = $w.Current.ProcessId
    if ([string]::IsNullOrWhiteSpace($name)) { continue }
    "$pid2`t$name" | Out-File $out -Append -Encoding utf8
}

"--- processes ---" | Out-File $out -Append -Encoding utf8
Get-Process | Where-Object { $_.MainWindowTitle } |
    ForEach-Object { "$($_.Id)`t$($_.ProcessName)`t$($_.MainWindowTitle)" } |
    Out-File $out -Append -Encoding utf8

exit 0
