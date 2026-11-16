# Checks the designation the Create dialog picks, without touching anything:
# starts ui-open-dialog.ps1 as a job (a separate process in the same session, so
# it sees the running object table), waits for the dialog, reads the designation,
# presses Close, and lets the opener close the part without saving.
#
# Run in session 1:
#   powershell -ExecutionPolicy Bypass -File ui-check-designation.ps1 <pid> <part.ipt>

param(
    [Parameter(Mandatory = $true)][int] $InventorPid,
    [Parameter(Mandatory = $true)][string] $PartPath
)

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$out = 'C:\Temp\ui-read.txt'
"starting" | Out-File $out -Encoding utf8
function Note([string] $text) { "$(Get-Date -Format HH:mm:ss) $text" | Out-File $out -Append -Encoding utf8 }

$job = Start-Job -FilePath 'C:\Temp\ui-open-dialog.ps1' -ArgumentList $InventorPid, $PartPath
Note "started the opener as job $($job.Id)"

try {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, 'Create a Modelled Thread')

    $dialog = $null
    for ($i = 0; $i -lt 120; $i++) {
        $dialog = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($null -ne $dialog) { break }
        Start-Sleep -Milliseconds 500
    }

    if ($null -eq $dialog) {
        Note "no dialog appeared"
        exit 1
    }

    Note "dialog is open"

    $all = $dialog.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)

    $seenCombos = @{}
    foreach ($e in $all) {
        $type = $e.Current.ControlType.ProgrammaticName

        if ($type -eq 'ControlType.ComboBox') {
            $text = ''
            $value = $null
            if ($e.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref] $value)) {
                $text = $value.Current.Value
            }

            if ($text.Length -eq 0) {
                $legacy = $null
                if ($e.TryGetCurrentPattern([System.Windows.Automation.LegacyIAccessiblePattern]::Pattern, [ref] $legacy)) {
                    $text = $legacy.Current.Value
                }
            }

            $selected = ''
            $selection = $null
            if ($e.TryGetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern, [ref] $selection)) {
                foreach ($item in $selection.GetSelection()) {
                    $selected = $item.Current.Name
                }
            }

            if (-not $seenCombos.ContainsKey($text)) {
                $seenCombos[$text] = $true
                Note ("combo: text='{0}' selected='{1}'" -f $text, $selected)
            }
        }
        elseif ($type -eq 'ControlType.Text' -and $e.Current.Name -like 'Major*') {
            Note ("says: " + $e.Current.Name)
        }
    }

    # The dialog's Close button is the Cancel button, so pressing it is the same
    # as a user closing the dialog.
    $closeCondition = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Button)),
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty, 'Close')))

    $close = $dialog.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $closeCondition)
    if ($null -ne $close) {
        $invoke = $null
        if ($close.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref] $invoke)) {
            $invoke.Invoke()
            Note "pressed Close"
        }
    }
}
finally {
    Wait-Job $job -Timeout 60 | Out-Null
    if ($job.State -eq 'Running') {
        Note "the opener is still running; stopping it"
        Stop-Job $job
    }

    # The opener is a separate process, so its job can report a broken remoting
    # channel as it exits. Its own report is what says whether it finished.
    $last = ''
    if (Test-Path 'C:\Temp\ui-open.txt') {
        $last = (Get-Content 'C:\Temp\ui-open.txt' | Select-Object -Last 1)
    }

    Note ("opener's last note: " + $last)
    Remove-Job $job -Force
}
