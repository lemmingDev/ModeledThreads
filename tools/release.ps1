<#
.SYNOPSIS
    Builds everything a release ships and, if asked, signs it.

.DESCRIPTION
    Produces, in the output folder:

      ModeledThreads-Deploy.zip             the add-in, for end users
      ModeledThreads-Tester-2025-2026.zip   the add-in plus the checks, for testers
      ModeledThreads-Setup.exe              the installer, when Inno Setup is present
      ModeledThreads.cer                    the public signing certificate, when signing
      SHA256SUMS.txt                        one hash per file above

    The add-in is always compiled against the oldest supported release (2025) so
    a single DLL runs on 2025, 2026 and 2027. The harness and ThreadMath checks
    run first, so a broken build cannot become an asset.

    Signing is optional and uses the certificate given. Without one the files
    are simply not signed; nothing else changes.

.PARAMETER Version
    Version to stamp into the assembly and into the bundle's PackageContents.xml.

.PARAMETER InventorVersion
    Interop to compile against. Keep the default (2025) for shipped builds.

.PARAMETER OutputDir
    Where the files are written. Defaults to dist/ in the repository.

.PARAMETER CertPath
    PFX to sign with. Omit for an unsigned build.

.PARAMETER CertPassword
    Password of the PFX.

.PARAMETER SkipInstaller
    Do not try to build ModeledThreads-Setup.exe.
#>
[CmdletBinding()]
param(
    [string] $Version = '1.0.0',

    [ValidateSet('2025', '2026', '2027')]
    [string] $InventorVersion = '2025',

    [string] $OutputDir = '',

    [string] $CertPath = '',

    [string] $CertPassword = '',

    [switch] $SkipInstaller
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDir) { $OutputDir = [IO.Path]::Combine($root, 'dist') }
$OutputDir = [IO.Path]::GetFullPath($OutputDir)

function Step([string] $text) {
    Write-Host ''
    Write-Host "== $text"
}

function Get-SignTool {
    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (Test-Path -LiteralPath $kits) {
        $found = Get-ChildItem -LiteralPath $kits -Directory |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName 'x64\signtool.exe' } |
            Where-Object { Test-Path -LiteralPath $_ } |
            Select-Object -First 1

        if ($found) { return $found }
    }

    return $null
}

function Invoke-Sign([string[]] $files, [string] $pfx, [string] $password) {
    $tool = Get-SignTool
    if (-not $tool) {
        throw 'signtool.exe was not found. Install the Windows SDK, or build without -CertPath.'
    }

    foreach ($file in $files) {
        if (-not (Test-Path -LiteralPath $file)) { continue }

        Write-Host "Signing $(Split-Path -Leaf $file)"
        & $tool sign /fd SHA256 /f $pfx /p $password `
            /tr http://timestamp.digicert.com /td SHA256 $file
        if ($LASTEXITCODE -ne 0) { throw "signtool failed on $file (exit $LASTEXITCODE)." }
    }
}

# ---- build and test --------------------------------------------------------
Step 'Building the add-in and the harness'
& (Join-Path $root 'build.ps1') -InventorVersion $InventorVersion -Configuration Release -Version $Version
if ($LASTEXITCODE -ne 0) { throw "The build failed (exit $LASTEXITCODE)." }

Step 'Running the maths and catalogue checks'
$math = [IO.Path]::Combine($root, 'tools', 'ThreadMath', 'ThreadMath.csproj')
dotnet build $math -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "ThreadMath did not build (exit $LASTEXITCODE)." }
dotnet run --project $math -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw 'ThreadMath found a problem; not packaging it.' }

# ---- sign what goes inside the packages -----------------------------------
$bundle = [IO.Path]::Combine($root, 'bundle', 'ModeledThreads.bundle')
$bundleDll = [IO.Path]::Combine($bundle, 'Contents', 'ModeledThreads.dll')
$harnessDir = [IO.Path]::Combine($root, 'tools', 'TestHarness', 'bin', 'Release')
$harnessExe = Join-Path $harnessDir 'ThreadHarness.exe'

$signed = $false
if ($CertPath) {
    Step 'Signing the DLL and the harness'
    Invoke-Sign @($bundleDll, $harnessExe) $CertPath $CertPassword
    $signed = $true
}

# ---- stage the two zips ---------------------------------------------------
Step 'Staging the packages'
$stage = Join-Path $OutputDir 'stage'
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null

function Copy-Bundle([string] $destination) {
    $target = Join-Path $destination 'bundle'
    New-Item -ItemType Directory -Path $target | Out-Null
    Copy-Item -LiteralPath $bundle -Destination $target -Recurse -Force

    # The version Inventor reports comes from here, so a release carries its own.
    $manifest = [IO.Path]::Combine($target, 'ModeledThreads.bundle', 'PackageContents.xml')
    $text = Get-Content -LiteralPath $manifest -Raw
    $text = [regex]::Replace($text, 'AppVersion="[^"]*"', "AppVersion=`"$Version`"")
    $text = [regex]::Replace($text, '\bVersion="[^"]*"', "Version=`"$Version`"")
    Set-Content -LiteralPath $manifest -Value $text -Encoding UTF8
}

$deploy = Join-Path $stage 'ModeledThreads-Deploy'
New-Item -ItemType Directory -Path $deploy | Out-Null
Copy-Bundle $deploy
Copy-Item (Join-Path $root 'README.md') $deploy
Copy-Item (Join-Path $root 'LICENSE') $deploy
Copy-Item (Join-Path $root 'install.bat') $deploy
Copy-Item (Join-Path $root 'install.ps1') $deploy
Copy-Item (Join-Path $root 'installer.iss') $deploy
# So the README's pictures and its link to SIGNING.md work inside the zip.
Copy-Item (Join-Path $root 'docs') $deploy -Recurse

$tester = Join-Path $stage 'ModeledThreads-Tester'
$testerTest = Join-Path $tester 'test'
New-Item -ItemType Directory -Path $testerTest | Out-Null
New-Item -ItemType Directory -Path (Join-Path $tester 'report') | Out-Null
Copy-Bundle $tester
Copy-Item (Join-Path $root 'install.ps1') $tester

$testerSource = [IO.Path]::Combine($root, 'tools', 'tester')
Copy-Item (Join-Path $testerSource 'READ-THIS-FIRST.txt') $tester
Copy-Item (Join-Path $testerSource 'START-HERE.bat') $tester
Copy-Item (Join-Path $testerSource 'start-here.ps1') $tester
Copy-Item (Join-Path $testerSource 'run-tests.bat') $testerTest
Copy-Item (Join-Path $testerSource 'run-tests.ps1') $testerTest
Copy-Item (Join-Path $harnessDir 'ThreadHarness.exe') $testerTest
Copy-Item (Join-Path $harnessDir 'ThreadHarness.exe.config') $testerTest
Copy-Item (Join-Path $harnessDir 'Autodesk.Inventor.Interop.dll') $testerTest

# The harness must load the very DLL that install.bat puts on the machine.
Copy-Item $bundleDll $testerTest

if (Test-Path -LiteralPath $OutputDir) {
    foreach ($name in 'ModeledThreads-Deploy.zip', 'ModeledThreads-Tester-2025-2026.zip',
                      'ModeledThreads-Setup.exe', 'ModeledThreads.cer', 'SHA256SUMS.txt') {
        $old = Join-Path $OutputDir $name
        if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old -Force }
    }
}

Step 'Making the zips'
Compress-Archive -Path $deploy -DestinationPath (Join-Path $OutputDir 'ModeledThreads-Deploy.zip') -Force
Compress-Archive -Path $tester -DestinationPath (Join-Path $OutputDir 'ModeledThreads-Tester-2025-2026.zip') -Force

# ---- installer ------------------------------------------------------------
$setup = Join-Path $OutputDir 'ModeledThreads-Setup.exe'
if (-not $SkipInstaller) {
    Step 'Building the installer'
    $iscc = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($iscc) {
        & $iscc.Source "/DAppVersion=$Version" "/O$OutputDir" (Join-Path $root 'installer.iss')
        if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed (exit $LASTEXITCODE)." }
    }
    else {
        Write-Warning 'iscc.exe (Inno Setup) was not found, so no installer was built.'
    }
}

if ($CertPath -and (Test-Path -LiteralPath $setup)) {
    Step 'Signing the installer'
    Invoke-Sign @($setup) $CertPath $CertPassword
}

# ---- certificate and checksums -------------------------------------------
if ($signed) {
    $certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($CertPath, $CertPassword)
    [IO.File]::WriteAllBytes(
        (Join-Path $OutputDir 'ModeledThreads.cer'),
        $certificate.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert))
}

Step 'Writing SHA256SUMS.txt'
$lines = @()
foreach ($file in Get-ChildItem -LiteralPath $OutputDir -File | Sort-Object Name) {
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    $lines += ('{0}  {1}' -f $hash.ToLowerInvariant(), $file.Name)
    Write-Host ('  {0}  {1}' -f $file.Name, $hash.ToLowerInvariant())
}

Set-Content -LiteralPath (Join-Path $OutputDir 'SHA256SUMS.txt') -Value $lines -Encoding ASCII

Remove-Item -LiteralPath $stage -Recurse -Force

Step 'Done'
Write-Host "Release $Version is in $OutputDir"
if (-not $signed) { Write-Host 'The files are NOT signed: no certificate was given.' }
