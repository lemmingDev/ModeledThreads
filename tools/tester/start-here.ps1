# One entry point for the tester: install the add-in, start Inventor, let them
# try it by hand, run the automated checks, and leave the add-in installed so it
# is there the next time they open Inventor.
#
# Started by START-HERE.bat. -SkipInstall is only for rehearsing the flow on a
# machine where the add-in is already in use; testers never need it.

param(
    [switch] $SkipInstall,
    [string] $InventorExe = ''
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

function Say([string] $text) { Write-Host $text }

# ---- which Inventor -------------------------------------------------------
if ($InventorExe.Length -gt 0) {
    if (-not (Test-Path -LiteralPath $InventorExe)) {
        Say "There is no Inventor at $InventorExe"
        exit 1
    }

    $found = @([pscustomobject]@{ Year = 'chosen'; Exe = $InventorExe })
}
else {
    $found = @()
    foreach ($year in 2026, 2025) {
        $exe = "C:\Program Files\Autodesk\Inventor $year\Bin\Inventor.exe"
        if (Test-Path -LiteralPath $exe) {
            $found += [pscustomobject]@{ Year = $year; Exe = $exe }
        }
    }
}

if ($found.Count -eq 0) {
    Say 'This pack is for Inventor 2025 and 2026, and neither was found in'
    Say 'C:\Program Files\Autodesk\.'
    Say ''
    Say 'Inventor 2027 is already covered, so there is nothing to test here.'
    Say 'If your Inventor is installed somewhere else, tell whoever gave you this.'
    exit 1
}

if ($found.Count -eq 1) {
    $choice = $found[0]
}
else {
    Say 'Found more than one Inventor:'
    foreach ($item in $found) { Say ("  {0}   {1}" -f $item.Year, $item.Exe) }
    Say ''
    $answer = Read-Host 'Which one do you want to test? Type 2026 or 2025 (Enter for 2026)'
    if ([string]::IsNullOrWhiteSpace($answer)) { $answer = '2026' }

    $choice = $found | Where-Object { $_.Year -eq $answer.Trim() } | Select-Object -First 1
    if ($null -eq $choice) {
        Say "There is no Inventor $answer here. Run this again and pick one of the years listed."
        exit 1
    }
}

Say ''
Say ("Testing Inventor {0}." -f $choice.Year)

# ---- install --------------------------------------------------------------
if (-not $SkipInstall) {
    while (Get-Process -Name Inventor -ErrorAction SilentlyContinue) {
        Say ''
        Say 'Inventor is running. Close it (save your work first), then press Enter.'
        Read-Host | Out-Null
    }

    Say ''
    Say 'Installing the add-in for your Windows account...'
    try {
        & (Join-Path $root 'install.ps1')
    }
    catch {
        Say ''
        Say ('The add-in could not be installed: ' + $_.Exception.Message)
        Say 'Send this message back with the report.'
        exit 1
    }
}
else {
    Say '(skipping the install step)'
}

# ---- start Inventor -------------------------------------------------------
$process = Get-Process -Name Inventor -ErrorAction SilentlyContinue |
    Sort-Object StartTime -Descending |
    Select-Object -First 1

if ($null -eq $process) {
    Say ''
    Say 'Starting Inventor. It takes a minute or two the first time.'
    $process = Start-Process -FilePath $choice.Exe -PassThru

    for ($i = 0; $i -lt 150; $i++) {
        Start-Sleep -Seconds 2
        if ($process.HasExited) {
            Say 'Inventor closed while it was starting. Run START-HERE again.'
            exit 1
        }

        $process.Refresh()
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) { break }
    }
}

# The checks reach Inventor through the running object table, which is filled in
# a little after the window appears.
Say 'Waiting for Inventor to be ready...'
for ($i = 0; $i -lt 60; $i++) {
    try {
        $null = [Runtime.InteropServices.Marshal]::GetActiveObject('Inventor.Application')
        break
    }
    catch {
        Start-Sleep -Seconds 2
    }
}
Start-Sleep -Seconds 3

# ---- try it by hand -------------------------------------------------------
Say ''
Say '--------------------------------------------------------------------'
Say 'Inventor is ready. Please try the add-in by hand first:'
Say ''
Say '  1. Open a part with a shaft or a plain hole (a copy, not work you need).'
Say '  2. Select the cylindrical face, then Modeled Threads > Create Thread.'
Say '     If the tab shows one small "Threads" button, double-click any tab'
Say '     to expand the ribbon.'
Say '  3. Check that the size box spells out the pitch, e.g. M10x1.5 not M10.'
Say '  4. Press Create Thread, look at the result, then double-click the'
Say '     thread node in the browser to edit it.'
Say '  5. Close the part without saving.'
Say ''
Say 'Write down anything odd: a crash, an error box, a thread in the wrong'
Say 'place, a damaged face, or a size box showing a bare M10.'
Say '--------------------------------------------------------------------'
Read-Host 'Press Enter when you have finished (or to skip straight to the checks)'

# ---- automated checks -----------------------------------------------------
& (Join-Path $root 'test\run-tests.ps1')

# ---- what they are left with ---------------------------------------------
Say ''
Say '--------------------------------------------------------------------'
Say 'Modeled Threads is installed and stays installed: it will be on the'
Say 'ribbon the next time you start Inventor, and you can use it normally.'
Say ''
Say 'To remove it later, close Inventor and delete this folder:'
Say ''
Say '  %APPDATA%\Autodesk\ApplicationPlugins\ModeledThreads.bundle'
Say '--------------------------------------------------------------------'
