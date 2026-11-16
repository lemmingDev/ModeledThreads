# Opens the user's part in the running Inventor, from the interactive session.
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$out = 'C:\Temp\open-part.txt'
"opening" | Out-File $out -Encoding utf8

try {
    $inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')
}
catch {
    "no running Inventor: $($_.Exception.Message)" | Out-File $out -Append -Encoding utf8
    exit 1
}

$path = 'C:\Mac\Home\Documents\thread.ipt'
$doc = $null
foreach ($d in $inv.Documents) {
    if ($d.FullFileName -eq $path) { $doc = $d; break }
}

if ($null -eq $doc) {
    "$(Get-Date -Format HH:mm:ss) opening $path" | Out-File $out -Append -Encoding utf8
    $doc = $inv.Documents.Open($path, $true)
}
else {
    "$(Get-Date -Format HH:mm:ss) already open" | Out-File $out -Append -Encoding utf8
}

$doc.Activate()
$inv.ActiveDocument = $doc
Start-Sleep -Seconds 2
$inv.ActiveView.Fit()
Start-Sleep -Seconds 1

"document: $($doc.DisplayName), dirty=$($doc.Dirty)" | Out-File $out -Append -Encoding utf8

$definition = $doc.ComponentDefinition
foreach ($feature in $definition.Features) {
    "feature: $($feature.Name)" | Out-File $out -Append -Encoding utf8
}

"volume: $([math]::Round($definition.MassProperties.Volume * 1000, 1)) mm3" | Out-File $out -Append -Encoding utf8
"done" | Out-File $out -Append -Encoding utf8
exit 0
