# Code signing

Patch Pal releases are signed with [Azure Artifact Signing](https://learn.microsoft.com/azure/artifact-signing/quickstart) (formerly Trusted Signing). The certificate is issued to the maintainer as an individual developer, so Windows shows the maintainer's name as the publisher.

Releases up to v2.2.0 are unsigned.

Only `PatchPal.exe` and the installers need signing. The WPF native DLLs that ship beside the exe (`*_cor3.dll`) are already signed by Microsoft.

Signing removes the "Unknown publisher" warning from UAC prompts. SmartScreen's "Windows protected your PC" warning fades as signed downloads build reputation; no certificate skips that step.

---

## How CI signs a release

`.github/workflows/release.yml` signs in two passes with [`azure/artifact-signing-action`](https://github.com/Azure/artifact-signing-action):

1. After `dotnet publish`: both `PatchPal.exe` builds (x64 and arm64), so the installers and portable zips carry the signed exe.
2. After Inno Setup: both `PatchPal-Setup-*.exe` installers.

`SHA256SUMS.txt` is generated afterwards, from the signed files. A check step fails the build if any of those files lacks a valid signature.

Both passes run only when the repository variable `ARTIFACT_SIGNING_ACCOUNT` is set; without it, releases build unsigned as before. CI signs in to Azure with OpenID Connect, so no Azure password or client secret is stored in GitHub.

---

## One-time setup

Individual developers must live in the US or Canada. Billing starts when the signing account is created and is not prorated.

1. **Azure subscription.** Use one whose billing account type is *Individual*; individual identity validation reads its details from that billing account.
2. **Register the resource provider** `Microsoft.CodeSigning` on the subscription.
3. **Create an Artifact Signing account** (Basic SKU is plenty) in a region near you. Note its name and the region's endpoint, for example `https://eus.codesigning.azure.net/` for East US.
4. **Assign yourself the role** *Artifact Signing Identity Verifier* on the account, then in the portal open **Identity validation → New identity → Individual**. Validation can only be done in the portal. Microsoft emails a link to finish the check with a government ID; sign in to it with the same email address as on the request.
5. **Create a certificate profile** of type *Public Trust* once validation succeeds. Note its name.
6. **Let GitHub Actions sign:**
   - In Microsoft Entra ID, create an app registration (for example `patch-pal-signing`).
   - Under **Certificates & secrets → Federated credentials**, add a GitHub Actions credential for `adrian3092/patch-pal`, entity type **Environment**, name `release`. The workflow's build job runs in that environment for both tag runs and runs started by hand. In GitHub (Settings → Environments → `release`), limit it to the `main` branch and `v*` tags so other branches can't sign.
   - On the signing account, give that app the role *Artifact Signing Certificate Profile Signer*.
7. **Add to the GitHub repo** (Settings → Secrets and variables → Actions):
   - Secrets: `AZURE_CLIENT_ID` (the app's client ID), `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`.
   - Variables: `ARTIFACT_SIGNING_ENDPOINT`, `ARTIFACT_SIGNING_ACCOUNT`, `ARTIFACT_SIGNING_PROFILE`.
8. **Test** with a manual run of the Release workflow from `main`, then check the downloaded exe as shown below before tagging a release.

### Known limitation

Inno Setup's uninstaller (`unins000.exe`) is generated and compressed inside the installer, so it stays unsigned. Uninstalling therefore shows an "Unknown publisher" UAC prompt.

---

## Verifying a signature

```powershell
Get-AuthenticodeSignature .\PatchPal.exe | Format-List *
Get-AuthenticodeSignature .\PatchPal-Setup-2.3.0-x64.exe | Format-List *
```

A trusted signature shows `Status: Valid` and a non-empty `TimeStamperCertificate`.
