# Lists the ribbons, tabs, panels and controls, writing as it goes so a failure
# part way through still leaves the findings behind.

$ErrorActionPreference = 'Continue'
$log = 'C:\Temp\ribbon.txt'
"starting" | Out-File $log -Encoding utf8

try {
    $inv = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')
}
catch {
    "Inventor could not be reached: $_" | Out-File $log -Append -Encoding utf8
    exit 1
}

$ribbons = $inv.UserInterfaceManager.Ribbons
"ribbons: $($ribbons.Count)" | Out-File $log -Append -Encoding utf8

$index = 0
foreach ($ribbon in $ribbons) {
    $index++
    "ribbon $index : internal='$($ribbon.InternalName)' active=$($ribbon.Active)" | Out-File $log -Append -Encoding utf8

    foreach ($tab in $ribbon.RibbonTabs) {
        $line = "  tab: '$($tab.InternalName)' / '$($tab.DisplayName)' visible=$($tab.Visible) active=$($tab.Active)"
        $line | Out-File $log -Append -Encoding utf8

        if ($tab.InternalName -ne 'ModeledThreads.Tab') { continue }

        foreach ($panel in $tab.RibbonPanels) {
            "    panel: '$($panel.InternalName)' / '$($panel.DisplayName)'" | Out-File $log -Append -Encoding utf8

            foreach ($control in $panel.CommandControls) {
                "      control: '$($control.InternalName)' / '$($control.DisplayName)' visible=$($control.Visible) type=$($control.ControlType)" | Out-File $log -Append -Encoding utf8
            }
        }
    }
}

"done" | Out-File $log -Append -Encoding utf8
