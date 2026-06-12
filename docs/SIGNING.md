# Code signing

This project ships unsigned by default. Signing is optional but it removes the **"Windows protected your PC"** SmartScreen prompt, which significantly improves first-run trust on a public download.

There are three realistic paths.

---

## Option 1 — SignPath.io free OSS signing (recommended)

[SignPath.io](https://signpath.io/) issues free code-signing certificates to qualifying open-source projects. They sign artifacts your CI produces, so your private key never leaves their infrastructure.

### Eligibility checklist

- [ ] Public GitHub repository
- [ ] OSI-approved license (MIT — already in this repo)
- [ ] Active development (recent commits, responding issues)
- [ ] CI pipeline that produces release artifacts (GitHub Actions workflow already in this repo)
- [ ] Reproducible builds (the included workflow qualifies)

### Application steps

1. Sign up at <https://signpath.io/> and choose the **Open Source** plan.
2. Submit your GitHub repo URL. Approval typically takes a few business days.
3. Once approved, in the SignPath UI:
   - Create a **Project** with slug `win-update-checker`.
   - Create an **Artifact configuration** for the installer (`WinUpdateChecker-Setup-*.exe`).
   - Create a **Signing policy** named `release-signing` that uses your certificate.
   - Generate a **CI API token** and add it to your GitHub repo secrets as `SIGNPATH_API_TOKEN`.
4. In `.github/workflows/release.yml`, uncomment the **"Submit signing request"** block and fill in your `organization-id`. Push a new tag — CI will upload the unsigned installer, SignPath signs it, the workflow downloads the signed result, and the release is published with a signed setup.exe.

### What gets signed

- The Inno Setup installers (`WinUpdateChecker-Setup-*-x64.exe` / `-arm64.exe`).
- Optionally the portable `WinUpdateChecker.exe` itself (configure a second artifact configuration).

---

## Option 2 — Buy a commercial certificate

If you want to sign locally without depending on SignPath, buy an OV ($200–400/yr) or EV ($300–600/yr) code-signing certificate from DigiCert, SSL.com, Sectigo, or similar. EV certificates skip the SmartScreen "reputation building" period entirely.

After installing the cert in `Cert:\CurrentUser\My`, sign the published exe with `signtool` (from the Windows SDK):

```powershell
signtool sign /n "Your Name" /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
  dist\publish-x64\WinUpdateChecker.exe
```

The RFC3161 timestamp (`/tr`) keeps the signature valid after the cert expires.

To sign the Inno Setup installer locally, configure Inno Setup's **Tools → Configure Sign Tools** and add a `SignTool` directive to `installer/setup.iss`.

---

## Option 3 — Self-signed certificate (development only)

For local testing of the signing pipeline:

```powershell
$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject "CN=WinUpdateChecker-Dev" `
    -CertStoreLocation Cert:\CurrentUser\My

# Trust it on this machine only:
$store = Get-Item Cert:\CurrentUser\Root
$store.Open('ReadWrite')
$store.Add($cert)
$store.Close()

signtool sign /n "WinUpdateChecker-Dev" /fd SHA256 dist\publish-x64\WinUpdateChecker.exe
```

A self-signed cert is **not** trusted by anyone else's machine. Useful only for verifying the signing flow before you have a real cert.

---

## Verifying a signature

```powershell
Get-AuthenticodeSignature .\WinUpdateChecker.exe | Format-List *
Get-AuthenticodeSignature .\WinUpdateChecker-Setup-2.0.0-x64.exe | Format-List *
```

A trusted signature shows `Status: Valid` and a non-empty `TimeStamperCertificate`.
