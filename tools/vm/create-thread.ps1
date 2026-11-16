# Cuts a modelled thread in the open part, the way the ribbon button does it:
# select the cylindrical face, press Create Thread, then press Create in the
# dialog. Written for the user's thread.ipt, whose counterbore is 30 mm across.
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$out = 'C:\Temp\create-thread.txt'
"starting" | Out-File $out -Encoding utf8
function Note([string] $text) { "$(Get-Date -Format HH:mm:ss) $text" | Out-File $out -Append -Encoding utf8 }

$inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')

$doc = $null
foreach ($d in $inv.Documents) {
    Note ("open document: '{0}' '{1}' visible={2}" -f $d.DisplayName, $d.FullFileName, $d.Visible)
    if ($d.FullFileName -like '*thread.ipt') { $doc = $d }
}

# A document opened with OpenVisible false has no window, cannot be made the
# active one, and the command then refuses to run. Open it again, visibly.
if ($null -ne $doc -and -not $doc.Visible) {
    Note "closing the invisible copy"
    $doc.Close($true)
    $doc = $null
}

if ($null -eq $doc) {
    Note "opening thread.ipt visibly"
    $doc = $inv.Documents.Open('C:\Mac\Home\Documents\thread.ipt', $true)
}

$doc.Activate()
$inv.ActiveDocument = $doc
Start-Sleep -Seconds 1
Note "document: $($doc.DisplayName), visible=$($doc.Visible), active: $($inv.ActiveDocument.DisplayName)"

$definition = $doc.ComponentDefinition

# What cylindrical faces are there, and which way each one faces.
$targets = @()
foreach ($body in $definition.SurfaceBodies) {
    foreach ($face in $body.Faces) {
        if ($face.SurfaceType -ne 5891) { continue }
        $cylinder = $face.Geometry
        # A face's normal points out of the material: out of a shaft, into a hole.
        $normal = $face.Evaluator.GetNormalAtPoint($face.PointOnFace)
        $dot = $normal.X * $cylinder.AxisVector.X + $normal.Y * $cylinder.AxisVector.Y + $normal.Z * $cylinder.AxisVector.Z
        $points = @()
        foreach ($edge in $face.Edges) { $points += $edge.PointOnEdge }
        $radius = [math]::Round($cylinder.Radius * 10, 3)
        Note ("face: r {0} mm, {1} edges, internal {2}" -f $radius, $face.Edges.Count, ($dot -lt 0))
        $targets += [pscustomobject]@{ Face = $face; Radius = $radius; Internal = ($dot -lt 0) }
    }
}

$target = $targets | Where-Object { $_.Internal -and [math]::Abs($_.Radius - 15.0) -lt 0.05 } | Select-Object -First 1
if ($null -eq $target) {
    $target = $targets | Where-Object { $_.Internal } | Sort-Object Radius -Descending | Select-Object -First 1
}

if ($null -eq $target) {
    Note "no hole to thread"
    exit 1
}

Note ("chosen: r {0} mm internal {1}" -f $target.Radius, $target.Internal)

$doc.SelectSet.Clear()
$doc.SelectSet.Select($target.Face)
Note ("selection: {0}" -f $doc.SelectSet.Count)

$button = $inv.CommandManager.ControlDefinitions.Item('ModeledThreads.Create')
$button.Execute()
Start-Sleep -Seconds 3

$root = [System.Windows.Automation.AutomationElement]::RootElement
$condition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, 'Create a Modelled Thread')
$dialog = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)

if ($null -eq $dialog) {
    Note "no dialog"
    exit 1
}

Note "dialog is open"

# What the dialog says about the face and the size it picked.
foreach ($e in $dialog.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.Condition]::TrueCondition)) {
    if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.Text' -and $e.Current.Name.Length -gt 20) {
        Note ("  says: " + ($e.Current.Name -replace "`r?`n", ' | '))
    }
}

$create = $null
foreach ($e in $dialog.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.Condition]::TrueCondition)) {
    if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.Button' -and $e.Current.Name -eq 'Create Thread') {
        $create = $e
        break
    }
}

if ($null -eq $create) {
    Note "no Create Thread button"
    exit 1
}

$create.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Note "Create Thread pressed"
Start-Sleep -Seconds 20

$dialogStillOpen = $null -ne $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
Note "dialog still open: $dialogStillOpen"

$definition = $doc.ComponentDefinition
foreach ($feature in $definition.Features) { Note "feature: $($feature.Name)" }
Note ("volume: {0} mm3" -f [math]::Round($definition.MassProperties.Volume * 1000, 1))

$logFile = 'C:\Users\jwill872\AppData\Local\ModeledThreads\modeled-threads.log'
if (Test-Path $logFile) {
    Get-Content $logFile -Tail 12 | ForEach-Object { Note ("  log: " + $_) }
}

$inv.ActiveView.Fit()
"done" | Out-File $out -Append -Encoding utf8
exit 0
