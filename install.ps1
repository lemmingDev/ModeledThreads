<#
.SYNOPSIS
    Installs the Modeled Threads add-in for the current user.

.DESCRIPTION
    Copies bundle/ModeledThreads.bundle into
    %APPDATA%\Autodesk\ApplicationPlugins, which is where Inventor looks for
    add-in bundles. No administrator rights are needed and nothing outside the
    user's own profile is touched.

    Inventor must be closed while this runs: it locks the DLL while it runs.

.PARAMETER Uninstall
    Remove the add-in instead of installing it.
#>
[CmdletBinding()]
param(
    [switch] $Uninstall
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$bundle = Join-Path $root 'bundle\ModeledThreads.bundle'
$target = Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins\ModeledThreads.bundle'

if (Get-Process -Name Inventor -ErrorAction SilentlyContinue) {
    throw 'Autodesk Inventor is running. Close it and run this again.'
}

if ($Uninstall) {
    if (Test-Path $target) {
        Remove-Item $target -Recurse -Force
        Write-Host "Removed $target"
    }
    else {
        Write-Host 'The add-in was not installed.'
    }

    return
}

if (-not (Test-Path $bundle)) {
    throw "The bundle is missing: $bundle. Run build.ps1 first."
}

$parent = Split-Path $target -Parent
if (-not (Test-Path $parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}

if (Test-Path $target) {
    Remove-Item $target -Recurse -Force
}

Copy-Item $bundle $target -Recurse -Force

$dll = Join-Path $target 'Contents\ModeledThreads.dll'
if (-not (Test-Path $dll)) {
    throw "The installation failed: $dll is missing."
}

$hash = (Get-FileHash $dll -Algorithm SHA256).Hash
Write-Host ''
Write-Host "Installed to $target"
Write-Host ("ModeledThreads.dll  {0:N0} bytes" -f (Get-Item $dll).Length)
Write-Host "SHA256              $hash"
Write-Host ''
Write-Host 'Start Inventor and use the "Modeled Threads" ribbon tab.'
