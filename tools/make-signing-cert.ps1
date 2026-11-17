<#
.SYNOPSIS
    Makes the self-signed certificate the releases are signed with.

.DESCRIPTION
    Run this once on Windows. It creates a code-signing certificate, keeps it in
    your own certificate store, and writes three files to certs/:

      ModeledThreads.pfx   the certificate and its private key, for signing
      ModeledThreads.cer   the public half, to give to people who want to trust it
      pfx-base64.txt       the .pfx as one base64 line, for the GitHub secret

    Then put pfx-base64.txt into the repository secret SIGNING_PFX_BASE64 and the
    password into SIGNING_PFX_PASSWORD, and the release workflow signs with it.
    The printed gh commands do that for you.

    The private key never leaves this machine except as those two secrets.

.PARAMETER Subject
    The name people will see. Keep "Modeled Threads" in it.

.PARAMETER OutDir
    Where the files go. Defaults to certs/ next to this script's repository.

.PARAMETER Years
    How long the certificate is valid for. Default 5.

.PARAMETER Password
    Password for the .pfx. Omit and a random one is made and printed. The .pfx
    needs one: an unpassworded one is a private key lying about.

.PARAMETER Force
    Replace files that are already in OutDir.
#>
[CmdletBinding()]
param(
    [string] $Subject = 'CN=Modeled Threads, O=Modeled Threads',

    [string] $OutDir = '',

    [ValidateRange(1, 30)]
    [int] $Years = 5,

    [string] $Password = '',

    [switch] $Force
)

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -ge 6 -and -not $IsWindows) {
    throw 'New-SelfSignedCertificate is Windows-only. Run this on Windows, or let the release workflow make an ephemeral certificate.'
}

if (-not $OutDir) {
    $OutDir = [IO.Path]::Combine($PSScriptRoot, '..', 'certs')
}

$OutDir = [IO.Path]::GetFullPath($OutDir)
$pfx = Join-Path $OutDir 'ModeledThreads.pfx'
$cer = Join-Path $OutDir 'ModeledThreads.cer'
$base64File = Join-Path $OutDir 'pfx-base64.txt'

if (-not (Test-Path -LiteralPath $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir | Out-Null
}

if (-not $Force) {
    foreach ($file in $pfx, $cer, $base64File) {
        if (Test-Path -LiteralPath $file) {
            throw "$file already exists. Pass -Force to replace it, and update the GitHub secrets if you do."
        }
    }
}

if (-not $Password) {
    $Password = [Convert]::ToBase64String(
        [Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
}

$secure = ConvertTo-SecureString -String $Password -AsPlainText -Force

Write-Host 'Creating the certificate...'
$certificate = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -KeyExportPolicy Exportable `
    -KeyUsage DigitalSignature `
    -KeyAlgorithm RSA `
    -KeyLength 3072 `
    -HashAlgorithm SHA256 `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -NotAfter (Get-Date).AddYears($Years)

Export-PfxCertificate -Cert $certificate -FilePath $pfx -Password $secure | Out-Null
Export-Certificate -Cert $certificate -FilePath $cer | Out-Null

# Base64 has no line breaks in a secret, so the workflow can decode it directly.
$bytes = [IO.File]::ReadAllBytes($pfx)
[IO.File]::WriteAllText($base64File, [Convert]::ToBase64String($bytes), [Text.Encoding]::ASCII)

Write-Host ''
Write-Host "Certificate : $($certificate.Subject)"
Write-Host "Thumbprint  : $($certificate.Thumbprint)"
Write-Host "Valid until : $($certificate.NotAfter.ToString('yyyy-MM-dd'))"
Write-Host ''
Write-Host "PFX         : $pfx"
Write-Host "Public part : $cer"
Write-Host ''
Write-Host 'Password for the .pfx (needed again, so keep it somewhere safe):'
Write-Host ''
Write-Host "  $Password"
Write-Host ''
Write-Host 'The hash and password are needed by the workflow. Set them with:'
Write-Host ''
Write-Host "  gh secret set SIGNING_PFX_BASE64 < $base64File"
Write-Host '  gh secret set SIGNING_PFX_PASSWORD'
Write-Host ''
Write-Host 'Or add them by hand under Settings > Secrets and variables > Actions.'
Write-Host ''
Write-Host 'To sign on this machine without GitHub, trust the certificate once:'
Write-Host ''
Write-Host "  Import-Certificate -FilePath $cer -CertStoreLocation Cert:\CurrentUser\Root"
Write-Host ''
Write-Host 'Then:'
Write-Host ''
Write-Host "  ./tools/release.ps1 -Version 1.0.0 -CertPath $pfx -CertPassword <password>"
