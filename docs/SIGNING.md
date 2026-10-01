# Code signing

Patch Pal is signed through the [SignPath Foundation](https://signpath.org) program, which gives open-source projects free code signing. The certificate is issued to SignPath Foundation, so Windows shows **SignPath Foundation** as the publisher. The project's commitments are in the README's [Code signing policy](../README.md#code-signing-policy).

Releases up to v2.0.0 are unsigned.

Signing removes the "Unknown publisher" warning from UAC prompts. SmartScreen's "Windows protected your PC" warning fades as signed downloads build reputation; no certificate skips that step any more (EV certificates stopped doing so in August 2024).

---

## Conditions to keep meeting

From the [SignPath Foundation terms](https://signpath.org/terms.html):

- An OSI-approved license (MIT) with no commercial dual-licensing and no proprietary components. A paid or closed edition of Patch Pal would end eligibility.
- Multi-factor authentication for every team member, on both GitHub and SignPath.
- The code signing policy stays on the README and is linked from each release's notes.
- Every release is approved by hand in SignPath before it is signed.
- Signed files carry the project name and release version: product name `Patch Pal` (set in `src/PatchPal.App/PatchPal.App.csproj` and `installer/setup.iss`) and the version CI passes from the tag.

---

## One-time setup

1. **Apply** at <https://signpath.org/apply>. Give the repository URL and the code signing policy URL (`https://github.com/adrian3092/win-update-checker#code-signing-policy`). Approval takes some days.
2. **After approval, in SignPath:**
   - Create the project `win-update-checker` and link the GitHub repository as its trusted build system.
   - Create two artifact configurations, each restricted to product name `Patch Pal` and the release version:
     - `exe` — signs the two `PatchPal.exe` builds (x64 and arm64) before they are packaged.
     - `installer` — signs the two `PatchPal-Setup-*.exe` installers.
   - Create the signing policy `release-signing` with manual approval.
   - Add a CI API token to the GitHub repo as the secret `SIGNPATH_API_TOKEN`, and the SignPath organization ID as the repository variable `SIGNPATH_ORGANIZATION_ID`.
3. **Wire up `.github/workflows/release.yml`** with two signing requests, using `signpath/github-action-submit-signing-request`:
   - After `dotnet publish`: sign `PatchPal.exe`, so the installers and portable zips both contain the signed exe.
   - After Inno Setup: sign the installers, then generate `SHA256SUMS.txt` from the signed files.

   Gate both steps on `SIGNPATH_ORGANIZATION_ID` so releases still build unsigned when it isn't set. Add a **Code signing policy** link to the release notes. Test with a manual `workflow_dispatch` run before tagging.

### Known limitation

Inno Setup's uninstaller (`unins000.exe`) is generated and compressed inside the installer, so it stays unsigned. Uninstalling an all-users install therefore shows an "Unknown publisher" UAC prompt.

---

## Alternative: Azure Artifact Signing

If Patch Pal ever needs a certificate in its own name (for example, for a paid edition), [Azure Artifact Signing](https://learn.microsoft.com/azure/artifact-signing/quickstart) costs $9.99/month. Individual developers must be in the US or Canada and verify their identity with a government ID; the certificate shows the developer's name and city, state, and country.

---

## Verifying a signature

```powershell
Get-AuthenticodeSignature .\PatchPal.exe | Format-List *
Get-AuthenticodeSignature .\PatchPal-Setup-2.0.0-x64.exe | Format-List *
```

A trusted signature shows `Status: Valid` and a non-empty `TimeStamperCertificate`.
