# Signing

Every release is signed: `ModeledThreads.dll` inside both zips, the harness, and
`ModeledThreads-Setup.exe`. The signature is what proves a download is the file
this repository's workflow built and that nobody has changed it since.

Windows does not trust the issuer yet. Until the project is accepted by
[SignPath's free signing for open-source projects](https://signpath.org/), the
certificate is self-signed, so Windows shows the publisher as unknown and keeps
its "are you sure" prompts. That is the honest state of it: the signature is
real and checkable, the *identity* behind it is not vouched for by a certificate
authority.

There are three things here:

1. [Checking a download](#checking-a-download) — for anyone who has one.
2. [Making and storing the certificate](#making-the-certificate) — once, by the
   maintainer.
3. [Moving to SignPath](#moving-to-signpath) — later, when the project qualifies.

## Checking a download

In PowerShell, in the folder the file was saved to:

```powershell
Get-FileHash .\ModeledThreads-Setup.exe -Algorithm SHA256
Get-AuthenticodeSignature .\ModeledThreads-Setup.exe | Format-List
```

The hash must match the line for that file in `SHA256SUMS.txt` from the same
release. The signature should name `CN=Modeled Threads` with a status of
`UnknownError` (self-signed) or `Valid` (once SignPath signs it). `NotSigned` or
a hash that does not match means the file is not the one that was published.

The zips are not signed themselves — Windows cannot sign a zip. Their contents
are, and `SHA256SUMS.txt` covers the zips, so the chain holds: the hash covers
the zip, the signatures cover what is inside it.

Every release also carries a GitHub build provenance attestation, which records
the commit and the workflow run that produced the files:

```bash
gh attestation verify ModeledThreads-Setup.exe --repo <owner>/ModeledThreads
```

### Trusting the certificate locally

To stop Windows warning about this publisher, import the public certificate from
the release once:

```powershell
Import-Certificate -FilePath .\ModeledThreads.cer -CertStoreLocation Cert:\CurrentUser\Root
```

That trusts this certificate **for your Windows account only**, and only for
what it signed. To undo it, delete the certificate from
`certmgr.msc` → *Trusted Root Certification Authorities* → *Certificates*.

Do this only if you got the `.cer` from the release page over HTTPS and the
signature on the file you are checking is otherwise sound. Trusting a
certificate is exactly as strong as the place it came from.

## Making the certificate

Once, on Windows, by whoever maintains the releases:

```powershell
pwsh ./tools/make-signing-cert.ps1
```

It creates a 3072-bit RSA code-signing certificate valid for five years, keeps
it in your own certificate store, and writes:

| File | What it is |
|---|---|
| `certs/ModeledThreads.pfx` | The certificate with its private key. **Never commit this.** |
| `certs/ModeledThreads.cer` | The public half, safe to publish. |
| `certs/pfx-base64.txt` | The `.pfx` as one base64 line, for the GitHub secret. |

It prints the password it made for the `.pfx`. Keep the password and the `.pfx`
somewhere safe: without them a release cannot be signed with the same
certificate, and everyone who trusted the old one would have to trust a new one.

`certs/` is in `.gitignore`. The private key exists in exactly two places: this
machine and the two GitHub secrets.

Then store them (the script prints these commands):

```bash
gh secret set SIGNING_PFX_BASE64   < certs/pfx-base64.txt
gh secret set SIGNING_PFX_PASSWORD
```

Or add them by hand under **Settings → Secrets and variables → Actions**. Both
are repository secrets, not environment secrets.

### What the workflow does with them

`.github/workflows/release.yml` decodes the `.pfx` into the runner's temporary
folder, signs with `signtool` and a DigiCert timestamp, and then signs the
installer with the same certificate. The temporary folder is discarded with the
runner; the secrets are never written to a log or an artifact.

Without the secrets the workflow still signs, using a certificate it makes for
that one run. Those releases are still tamper-evident, but each one has a
different issuer, so do not ask anyone to trust them — the self-signed
certificate in `certs/` is the one worth trusting.

`signtool` timestamps every signature. A timestamp is what keeps a signature
valid after the certificate expires: the signature records when it was made, so
an old release stays verifiable. The certificate is good for five years, and
releases signed with it stay verifiable after that.

### If the certificate is lost or must be replaced

Make a new one with `-Force`, update both secrets, and say so in the release
notes. Also delete the old `ModeledThreads.cer` guidance from older releases, or
people will keep trusting a certificate that no longer signs anything.

## Moving to SignPath

[SignPath Foundation](https://signpath.org/) signs open-source projects for
free, with a certificate Windows already trusts, so the publisher reads as
**SignPath Foundation** and the "unknown publisher" prompt goes away. It needs:

- a public repository (this one),
- an OSI-approved licence (MIT, in `LICENSE`),
- a project that is genuinely used or useful to others.

The application is a short form on their site; approval is by hand and can take
a few weeks. Nothing in this repository has to change to apply.

Once it is accepted, the changes are small and all in
`.github/workflows/release.yml`:

1. Add the three secrets SignPath gives you: `SIGNPATH_API_TOKEN`,
   `SIGNPATH_ORGANIZATION_ID`, `SIGNPATH_PROJECT_SLUG`.
2. Replace the `Prepare the signing certificate` and the signing part of
   `Build, check, sign and package` with SignPath's GitHub action. Their action
   takes the files, signs them in their service with a hardware-backed key, and
   returns them; the private key never exists in this repository or on a runner.
3. Keep `tools/release.ps1` as it is for local, unsigned builds: pass no
   `-CertPath` and it packages without signing.

The order matters: SignPath signs after the build and before the zips are made,
exactly where `release.ps1` signs now. The SDK and the full example are at
<https://github.com/SignPath/github-action-submit-signing-request>.

Then update this file and the release notes template in the workflow: the
sentence about the self-signed certificate should go, because Windows will trust
the signature.

## What is not signed, and why that is fine

- **The zips.** ZIP has no signature slot. `SHA256SUMS.txt` is the substitute.
- **`SHA256SUMS.txt` itself.** It is published over HTTPS from the release page,
  and its contents are checkable against the signatures on the files inside.
- **`README.md`, the tester scripts, and the guide.** They carry no secrets and
  do nothing but copy files; the DLL and the harness they act on are signed.
