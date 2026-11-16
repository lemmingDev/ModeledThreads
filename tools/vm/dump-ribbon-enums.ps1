# Prints the Inventor enum names behind the ribbon's numeric state values, so a
# probe can be read without guessing which mode 83204 means.
$asm = [Reflection.Assembly]::LoadFrom('C:\Temp\doctor\Autodesk.Inventor.Interop.dll')

$lines = @()
foreach ($name in 'RibbonStateEnum', 'RibbonAppearanceEnum', 'RibbonDockingStateEnum', 'InterfaceStyleEnum') {
    $type = $asm.GetType("Inventor.$name")
    if ($null -eq $type) { $lines += "no $name"; continue }
    $lines += "== $name"
    foreach ($value in [Enum]::GetValues($type)) {
        $lines += ("   {0} = {1}" -f $value, [int]$value)
    }
}

$lines | Out-File 'C:\Temp\enums.txt' -Encoding utf8
