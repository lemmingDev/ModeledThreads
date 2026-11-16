# Runs the automated checks against the Inventor that is already open, and
# writes one report file that can be sent back.
#
# Started by run-tests.bat. Needs Inventor open, and does not touch any document
# the tester has open: the checks build their own parts and close them without
# saving.

$ErrorActionPreference = 'Continue'

$root = Split-Path -Parent $PSScriptRoot
$reportDir = Join-Path $root 'report'
if (-not (Test-Path -LiteralPath $reportDir)) {
    New-Item -ItemType Directory -Path $reportDir | Out-Null
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$report = Join-Path $reportDir ("ModeledThreads-report-{0}-{1}.txt" -f $env:COMPUTERNAME, $stamp)

function Say([string] $text) {
    Write-Host $text
    Add-Content -LiteralPath $report -Value $text -Encoding UTF8
}

Say 'Modeled Threads - test report'
Say ('Written {0}' -f (Get-Date))
Say ''

$process = Get-Process -Name Inventor -ErrorAction SilentlyContinue |
    Sort-Object StartTime -Descending |
    Select-Object -First 1

if ($null -eq $process) {
    Say 'Inventor is not running.'
    Say ''
    Say 'Start Inventor (2025 or 2026), wait until it has finished loading,'
    Say 'then run this again. Nothing was tested.'
    Write-Host ''
    Write-Host "The empty report is in $report"
    exit 1
}

$exe = $process.Path
$version = (Get-Item -LiteralPath $exe).VersionInfo

Say ("Computer:        {0}" -f $env:COMPUTERNAME)
Say ("Windows:         {0} {1}" -f
    (Get-CimInstance Win32_OperatingSystem).Caption,
    (Get-CimInstance Win32_OperatingSystem).Version)
Say ("Inventor:        {0}" -f $version.ProductName)
Say ("Inventor exe:    {0}" -f $exe)
Say ("File version:    {0}" -f $version.FileVersion)
Say ("Product version: {0}" -f $version.ProductVersion)
Say ("Process id:      {0}" -f $process.Id)

$release = Split-Path -Leaf (Split-Path -Parent (Split-Path -Parent $exe))
Say ("Install folder:  {0}" -f $release)

$bundle = Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins\ModeledThreads.bundle'
$bundleDll = Join-Path $bundle 'Contents\ModeledThreads.dll'
if (Test-Path -LiteralPath $bundleDll) {
    $hash = (Get-FileHash -LiteralPath $bundleDll -Algorithm SHA256).Hash
    Say ("Add-in DLL:      {0}" -f $bundleDll)
    Say ("Add-in SHA-256:  {0}" -f $hash)
}
else {
    Say 'Add-in DLL:      not installed (run install.bat first)'
}

Say ''
Say 'The add-in should be on the "Modeled Threads" ribbon tab.'
Say ''
Say 'Running the checks. This takes a few minutes; leave Inventor alone'
Say 'until it says it has finished.'
Say ''

$harness = Join-Path $PSScriptRoot 'ThreadHarness.exe'
$output = & $harness --pid $process.Id 2>&1
$code = $LASTEXITCODE

foreach ($line in $output) {
    $text = [string]$line
    Add-Content -LiteralPath $report -Value $text -Encoding UTF8
    Write-Host $text
}

Say ''
Say ("The checks exited with code {0}." -f $code)

if ($code -eq 0) {
    Say 'RESULT: every check passed on this Inventor.'
}
else {
    Say 'RESULT: at least one check failed. The lines marked FAIL say which.'
}

$log = Join-Path $env:LOCALAPPDATA 'ModeledThreads\modeled-threads.log'
if (Test-Path -LiteralPath $log) {
    Say ''
    Say '---- last 60 lines of the add-in log ----'
    foreach ($line in (Get-Content -LiteralPath $log -Tail 60)) {
        Say $line
    }

    $logCopy = Join-Path $reportDir ('modeled-threads-{0}-{1}.log' -f $env:COMPUTERNAME, $stamp)
    Copy-Item -LiteralPath $log -Destination $logCopy -Force
    Say ''
    Say ("The whole log was copied to {0}" -f $logCopy)
}

Say ''
Say 'Finished. Send the report file (and the log copy, if there is one) back.'
