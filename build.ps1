<#
.SYNOPSIS
    Builds the Modeled Threads add-in and refreshes the ready-to-install bundle.

.DESCRIPTION
    Needs a .NET SDK on the build machine (10.0 or newer: the harness builds with
    6.0, but tools/ThreadMath targets net10.0). The add-in itself
    targets .NET Framework 4.8, which every Windows 10 and 11 machine already
    has, so nothing has to be installed on the machine that runs Inventor.

    The add-in is compiled against the Autodesk.Inventor.Interop in
    refs/<version>; without it the build falls back to the Autodesk.Inventor.Sdk
    NuGet package.

    The built DLL is copied into bundle/ModeledThreads.bundle/Contents, so that
    folder is always the thing to install.

.PARAMETER InventorVersion
    Inventor release to compile against: 2025, 2026 or 2027 (default 2027).

.PARAMETER InventorInteropDir
    Explicit folder holding Autodesk.Inventor.Interop.dll, for building against
    an installed Inventor instead of the copy in refs/<version>.

.PARAMETER Configuration
    Debug (default) or Release.

.PARAMETER SkipHarness
    Do not build the COM test harness in tools/TestHarness.
#>
[CmdletBinding()]
param(
    [ValidateSet('2025', '2026', '2027')]
    [string] $InventorVersion = '2027',

    [string] $InventorInteropDir = '',

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [switch] $SkipHarness
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

function Find-Dotnet {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    foreach ($candidate in @("$env:ProgramFiles\dotnet\dotnet.exe", "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe")) {
        if (Test-Path $candidate) { return $candidate }
    }

    throw 'dotnet was not found. Install a .NET SDK: https://dotnet.microsoft.com/download'
}

$dotnet = Find-Dotnet
Write-Host "Using $dotnet"

$common = @('--nologo', '-v', 'minimal', '-c', $Configuration,
            "-p:InventorVersion=$InventorVersion")
if ($InventorInteropDir) { $common += "-p:InventorInteropDir=$InventorInteropDir" }

$project = Join-Path $root 'src\ModeledThreads\ModeledThreads.csproj'
& $dotnet build $project @common
if ($LASTEXITCODE -ne 0) { throw "Add-in build failed with exit code $LASTEXITCODE." }

if (-not $SkipHarness) {
    $harness = Join-Path $root 'tools\TestHarness\TestHarness.csproj'
    & $dotnet build $harness @common
    if ($LASTEXITCODE -ne 0) { throw "Harness build failed with exit code $LASTEXITCODE." }
}

$contents = Join-Path $root 'bundle\ModeledThreads.bundle\Contents'
$built = Join-Path $root "src\ModeledThreads\bin\$Configuration"

Copy-Item (Join-Path $built 'ModeledThreads.dll') $contents -Force

$dll = Join-Path $contents 'ModeledThreads.dll'
if (-not (Test-Path $dll)) { throw "Build finished but $dll is missing." }

$info = Get-Item $dll
$hash = (Get-FileHash $dll -Algorithm SHA256).Hash
Write-Host ''
Write-Host "Bundle : $(Join-Path $root 'bundle\ModeledThreads.bundle')"
Write-Host ("DLL    : {0}  ({1:N0} bytes, {2:yyyy-MM-dd HH:mm})" -f $info.Name, $info.Length, $info.LastWriteTime)
Write-Host "SHA256 : $hash"
Write-Host ''
Write-Host 'Install with:  .\install.ps1'
