# WinUpdateChecker v2; Plan 3 of 3: Distribution & Migration

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship v2.0.0; self-contained single-exe publishing (x64 + ARM64), updated installer and CI release pipeline, rewritten README with the v1→v2 migration table, and retirement of the PowerShell implementation.

**Architecture:** No new app code; versioning plumbing, packaging scripts, CI, and docs. `dotnet publish` with `PublishSingleFile` (NEVER `PublishTrimmed`: WPF-UI navigation breaks under trimming). Two architecture-specific artifact sets (installer + portable zip per arch). The v1 installer's AppId is preserved so v2 upgrades replace v1 installs cleanly, with `[InstallDelete]` purging the obsolete v1 script files.

**Tech Stack:** dotnet publish, Inno Setup 6, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-06-10-wpf-rewrite-design.md` (Distribution, CI, migration section).

---

### Task 1: Version plumbing

Make the assembly version the single source of truth so CI can stamp releases with `-p:Version`.

**Files:**
- Modify: `src/WinUpdateChecker.Core/AppInfo.cs`, `src/WinUpdateChecker.Core/WinUpdateChecker.Core.csproj`, `src/WinUpdateChecker.App/WinUpdateChecker.App.csproj`
- Test: `tests/WinUpdateChecker.Core.Tests/AppInfoTests.cs` (new)

- [ ] **Step 1: Write the failing test**

```csharp
using WinUpdateChecker.Core;

namespace WinUpdateChecker.Core.Tests;

public class AppInfoTests
{
    [Fact]
    public void Version_ComesFromAssemblyMetadata()
    {
        // csproj sets <Version>2.0.0</Version>; CI overrides with -p:Version=x.y.z
        Assert.Equal("2.0.0", AppInfo.Version);
    }

    [Fact]
    public void Version_HasNoBuildMetadataSuffix()
        => Assert.DoesNotContain("+", AppInfo.Version);
}
```

- [ ] **Step 2: Run `dotnet test --filter AppInfoTests`**: PASS against the current const. These are characterization tests: they pin the version value so the refactor in Step 3 (const → assembly-metadata) is verified not to change behavior. (True RED isn't possible here; the const already returns "2.0.0".)

- [ ] **Step 3: Implement**

In both csproj files, add to the first `<PropertyGroup>`:

```xml
<Version>2.0.0</Version>
```

In `AppInfo.cs`, replace the `Version` const with assembly-derived:

```csharp
using System.Reflection;

namespace WinUpdateChecker.Core;

public static class AppInfo
{
    public const string ProductName = "WinUpdateChecker";
    public const string RepoUrl = "https://github.com/adrian3092/win-update-checker";

    /// <summary>From the assembly's InformationalVersion (csproj &lt;Version&gt;, CI -p:Version),
    /// with any "+commit" build-metadata suffix stripped.</summary>
    public static string Version { get; } =
        typeof(AppInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0]
        ?? "0.0.0-dev";
}
```

- [ ] **Step 4: `dotnet test --filter AppInfoTests`**: PASS; full suite green (146).

- [ ] **Step 5: Commit**: `git add src tests` / `git commit -m "feat: derive app version from assembly metadata"`

---

### Task 2: Verify self-contained publish locally

**Files:** none (verification only; fixes if publish breaks).

- [ ] **Step 1: Publish x64**

```powershell
dotnet publish src/WinUpdateChecker.App -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o dist/publish-x64
```

Expected: succeeds; `dist/publish-x64/WinUpdateChecker.exe` exists (~70-100 MB); at most a handful of sidecar files (a `.pdb` is fine; exclude it from packaging later).

- [ ] **Step 2: Smoke the published exe**

```powershell
.\dist\publish-x64\WinUpdateChecker.exe --source npm; echo "exit=$LASTEXITCODE"
```

Expected: error message + `exit=1` (proves the single file runs standalone). Then launch the GUI from the published exe, wait ~10 s, screenshot via `tools/Capture-Window.ps1`, READ it (window renders, no crash), kill the process.

- [ ] **Step 3: Publish arm64 (build-only check; can't execute on x64)**

```powershell
dotnet publish src/WinUpdateChecker.App -c Release -r win-arm64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o dist/publish-arm64
```

Expected: succeeds; exe exists.

- [ ] **Step 4:** Nothing to commit (dist/ is gitignored). Report sizes of both exes.

---

### Task 3: Rewrite the installer

**Files:**
- Modify: `installer/setup.iss`

- [ ] **Step 1: Replace setup.iss with the v2 script**

```iss
; WinUpdateChecker installer script (Inno Setup 6).
; CI builds one installer per architecture:
;   ISCC.exe /DMyAppVersion=2.0.0 /DMyAppArch=x64   /DMySourceDir=..\dist\publish-x64   installer\setup.iss
;   ISCC.exe /DMyAppVersion=2.0.0 /DMyAppArch=arm64 /DMySourceDir=..\dist\publish-arm64 installer\setup.iss

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif
#ifndef MyAppArch
  #define MyAppArch "x64"
#endif
#ifndef MySourceDir
  #define MySourceDir "..\dist\publish-x64"
#endif

#define MyAppName        "WinUpdateChecker"
#define MyAppPublisher   "WinUpdateChecker contributors"
#define MyAppURL         "https://github.com/adrian3092/win-update-checker"
#define MyAppExeName     "WinUpdateChecker.exe"

[Setup]
; Same AppId as v1 so installing v2 upgrades an existing v1 install in place.
AppId={{60cbe8cd-e316-4bc8-9a92-96305ec2c7d2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename=WinUpdateChecker-Setup-{#MyAppVersion}-{#MyAppArch}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
#if MyAppArch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
UninstallDisplayName={#MyAppName} {#MyAppVersion}
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MySourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md";    DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE";      DestDir: "{app}"; Flags: ignoreversion
Source: "..\CHANGELOG.md"; DestDir: "{app}"; Flags: ignoreversion

[InstallDelete]
; Purge v1 (PowerShell) files when upgrading an existing install.
Type: files; Name: "{app}\UpdateChecker.ps1"
Type: files; Name: "{app}\Run.bat"
Type: files; Name: "{app}\Run-Console.bat"

[Icons]
Name: "{autoprograms}\{#MyAppName}";            Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autoprograms}\Uninstall {#MyAppName}";  Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}";             Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
```

- [ ] **Step 2: Local build check (best-effort)**: if `C:\Program Files (x86)\Inno Setup 6\ISCC.exe` exists, run it with the x64 defines against the Task 2 publish output and confirm `dist/WinUpdateChecker-Setup-0.0.0-dev-x64.exe` is produced. If Inno Setup isn't installed locally, note it; CI installs it via choco.

- [ ] **Step 3: Commit**: `git add installer/setup.iss` / `git commit -m "feat(installer): install the v2 exe with per-arch builds and v1 cleanup"`

---

### Task 4: Rewrite the release workflow

**Files:**
- Modify: `.github/workflows/release.yml`

- [ ] **Step 1: Replace the workflow**

```yaml
name: Release

on:
  push:
    tags: ['v*']
  workflow_dispatch:
    inputs:
      version:
        description: 'Version (e.g. 2.0.0). Leave blank when building from a tag.'
        required: false

permissions:
  contents: write

jobs:
  test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Run tests
        run: dotnet test -warnaserror

  build:
    needs: test
    runs-on: windows-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Resolve version
        id: ver
        shell: pwsh
        run: |
          if ($env:GITHUB_REF -like 'refs/tags/v*') {
            $v = $env:GITHUB_REF -replace '^refs/tags/v',''
          } elseif ('${{ inputs.version }}') {
            $v = '${{ inputs.version }}'
          } else {
            $v = '0.0.0-ci'
          }
          "version=$v" | Out-File $env:GITHUB_OUTPUT -Append
          Write-Host "Building version: $v"

      - name: Publish (x64 and arm64)
        shell: pwsh
        run: |
          $v = '${{ steps.ver.outputs.version }}'
          foreach ($arch in 'x64','arm64') {
            dotnet publish src/WinUpdateChecker.App -c Release -r "win-$arch" --self-contained `
              -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
              -p:Version=$v -o "dist/publish-$arch"
            if ($LASTEXITCODE -ne 0) { exit 1 }
          }

      - name: Install Inno Setup
        shell: pwsh
        run: choco install innosetup --no-progress -y

      - name: Build installers
        shell: pwsh
        run: |
          $v = '${{ steps.ver.outputs.version }}'
          foreach ($arch in 'x64','arm64') {
            & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" `
              "/DMyAppVersion=$v" "/DMyAppArch=$arch" "/DMySourceDir=..\dist\publish-$arch" `
              installer\setup.iss
            if ($LASTEXITCODE -ne 0) { exit 1 }
          }

      - name: Build portable zips
        shell: pwsh
        run: |
          $v = '${{ steps.ver.outputs.version }}'
          foreach ($arch in 'x64','arm64') {
            $stage = "dist/portable-$arch"
            New-Item -ItemType Directory -Force -Path $stage | Out-Null
            Copy-Item "dist/publish-$arch/WinUpdateChecker.exe", 'README.md', 'LICENSE', 'CHANGELOG.md' $stage
            Compress-Archive -Path "$stage/*" -DestinationPath "dist/WinUpdateChecker-portable-$v-$arch.zip" -Force
          }

      - name: Generate SHA256SUMS
        shell: pwsh
        run: |
          Push-Location dist
          Get-ChildItem -File | Where-Object { $_.Extension -in '.exe','.zip' } | ForEach-Object {
            "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower())  $($_.Name)"
          } | Set-Content SHA256SUMS.txt -Encoding ASCII
          Get-Content SHA256SUMS.txt
          Pop-Location

      # ----------------------------------------------------------------------
      # OPTIONAL: SignPath integration. Uncomment after your project is
      # accepted at https://signpath.io/ and you have an artifact-config GUID.
      # ----------------------------------------------------------------------
      # - name: Submit signing request
      #   uses: signpath/github-action-submit-signing-request@v1
      #   with:
      #     api-token: ${{ secrets.SIGNPATH_API_TOKEN }}
      #     organization-id: '<your-org-id>'
      #     project-slug: 'win-update-checker'
      #     signing-policy-slug: 'release-signing'
      #     artifact-configuration-slug: 'installer'
      #     github-artifact-id: ${{ steps.upload.outputs.artifact-id }}
      #     wait-for-completion: true
      #     output-artifact-directory: 'dist-signed'
      # ----------------------------------------------------------------------

      - name: Upload artifacts
        id: upload
        uses: actions/upload-artifact@v4
        with:
          name: WinUpdateChecker-${{ steps.ver.outputs.version }}
          path: |
            dist/*.exe
            dist/*.zip
            dist/SHA256SUMS.txt

      - name: Create GitHub release
        if: startsWith(github.ref, 'refs/tags/')
        uses: softprops/action-gh-release@v2
        with:
          files: |
            dist/WinUpdateChecker-Setup-${{ steps.ver.outputs.version }}-x64.exe
            dist/WinUpdateChecker-Setup-${{ steps.ver.outputs.version }}-arm64.exe
            dist/WinUpdateChecker-portable-${{ steps.ver.outputs.version }}-x64.zip
            dist/WinUpdateChecker-portable-${{ steps.ver.outputs.version }}-arm64.zip
            dist/SHA256SUMS.txt
          draft: true
          generate_release_notes: true
```

- [ ] **Step 2: Validate YAML**: `python -c "import yaml,sys; yaml.safe_load(open('.github/workflows/release.yml'))"` (or any YAML check available). Expected: no parse error.

- [ ] **Step 3: Commit**: `git add .github/workflows/release.yml` / `git commit -m "ci: build per-arch self-contained releases with test gate"`

---

### Task 5: README rewrite + signing doc touch-up

**Files:**
- Modify: `README.md`, `docs/SIGNING.md` (mechanical reference fixes only)

- [ ] **Step 1: Rewrite README.md.** Keep the same overall personality and structure as the current README (read it first). Required content; write it out fully, adapting current text where it still applies:
  - Intro: native Windows 11-style app (C#/WPF, Fluent design) that scans installed programs and updates them via winget/Scoop/Chocolatey. One-click updates, update history, light/dark theme, zero telemetry, single self-contained exe (no .NET install needed). Note v1 was PowerShell; v2.0 is a full rewrite; link to CHANGELOG.
  - Features list: sidebar GUI (Updates / All apps / History / Settings), per-row and bulk updates with live progress, persisted update history with failure logs, CSV/HTML export via CLI, headless CLI for scheduled tasks, light/dark/system theme, x64 + ARM64.
  - Screenshots placeholder section (docs/screenshots/): keep.
  - Requirements: Windows 10 1809+ / Windows 11, x64 or ARM64; at least one of winget/Scoop/Chocolatey; **no .NET runtime needed** (self-contained).
  - Install: installer per arch (`WinUpdateChecker-Setup-x.y.z-x64.exe` / `-arm64.exe`) from latest release; portable zip per arch (unblock → extract → run `WinUpdateChecker.exe`). From source: `git clone` + `dotnet run --project src/WinUpdateChecker.App` (requires .NET 8 SDK).
  - Usage: GUI (just run the exe); CLI table of all five flags with descriptions and examples (`--no-gui`, `--export-csv <path>`, `--export-html <path>`, `--source winget,scoop`, `--include-system-components`); exit codes 0/1; note CLI is scan/export only; upgrades are GUI-only.
  - **Migrating from v1.x** section with this exact table plus a note that `UpdateChecker.ps1` is gone and scheduled tasks must be updated to call the exe:

    | v1 (PowerShell) | v2 |
    |---|---|
    | `.\Run.bat` | `WinUpdateChecker.exe` |
    | `-NoGui` | `--no-gui` |
    | `-ExportCsv <path>` | `--export-csv <path>` |
    | `-ExportHtml <path>` | `--export-html <path>` |
    | `-Source winget,scoop` | `--source winget,scoop` |
    | `-IncludeSystemComponents` | `--include-system-components` |

  - Scheduled-task example updated to invoke the installed exe with `--export-html`.
  - How it works: keep the v1 ASCII diagram concept, updated labels (RegistryScanner → sources in parallel → matcher → GUI/CLI). Mention winget/choco upgrades elevate via UAC; Scoop runs as the user.
  - FAQ: carry over the still-true entries (does it modify anything on its own; SmartScreen; "Up to date / unknown"; store-managed apps; admin question), drop PowerShell-specific phrasing, add "Why is the download ~80 MB?" (self-contained .NET runtime; no prerequisites).
  - Verify a release: keep (per-arch artifact names in the example).
  - Building releases: local `dotnet publish` + ISCC command lines (matching Task 3 header comments), CI on tag push.
  - Contributing: open an issue first; .NET 8 SDK; `dotnet test` must pass; keep Core UI-free.
  - License: MIT.
- [ ] **Step 2: docs/SIGNING.md**: read it; mechanically update any references to `UpdateChecker.ps1`/script signing so they refer to the v2 exe/installer artifacts. Don't restructure the document.
- [ ] **Step 3: Commit**: `git add README.md docs/SIGNING.md` / `git commit -m "docs: rewrite README for v2 with migration guide"`

---

### Task 6: Retire v1 and finalize CHANGELOG

**Files:**
- Delete: `UpdateChecker.ps1`, `Run.bat`, `Run-Console.bat`, `tests/Merge.Tests.ps1`, `tools/Sign-Script.ps1` (PS-script signing helper; superseded; check nothing else references it first)
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Check references**: `grep -ri "UpdateChecker.ps1\|Run-Console\|Sign-Script" --include="*.md" --include="*.yml" --include="*.iss" .` (excluding docs/superpowers/ and CHANGELOG history entries, which legitimately mention v1). Any live reference in README/workflow/installer means an earlier task missed something; fix there first.
- [ ] **Step 2: Delete**: `git rm UpdateChecker.ps1 Run.bat Run-Console.bat tests/Merge.Tests.ps1 tools/Sign-Script.ps1`
- [ ] **Step 3: CHANGELOG**: convert the `[Unreleased]` section into `## [2.0.0] - <today's date>` and expand it:

```markdown
## [2.0.0] - 2026-06-12

Complete rewrite: WinUpdateChecker is now a native C#/.NET 8 application.

### Added
- Fluent (Windows 11-style) GUI with sidebar navigation: Updates, All apps, History, Settings.
- Per-row and bulk updates with live in-place progress and result toasts.
- Persisted update history (%APPDATA%\WinUpdateChecker) with captured failure logs.
- Settings page: theme (System/Light/Dark), scan on launch, per-source toggles,
  system-components toggle.
- Self-contained single-exe builds for x64 and ARM64; no PowerShell or .NET install needed.
- Parallel source queries; scans are faster than v1.

### Changed
- **Breaking:** CLI flags renamed (`-NoGui` → `--no-gui`, `-ExportCsv` → `--export-csv`,
  `-ExportHtml` → `--export-html`, `-Source` → `--source`,
  `-IncludeSystemComponents` → `--include-system-components`). Update scheduled tasks
  to invoke `WinUpdateChecker.exe`: see the README migration table.
- Failed package-manager queries now surface as visible warnings instead of being
  silently treated as "no updates".

### Removed
- **Breaking:** `UpdateChecker.ps1`, `Run.bat`, and `Run-Console.bat`. The installer
  removes them from existing installs on upgrade.
```

(Adjust the date to the actual day; keep the existing 1.x entries below untouched.)

- [ ] **Step 4: Full verification**: `dotnet build -warnaserror` + `dotnet test` (146 green). Run `dotnet run --project src/WinUpdateChecker.App -- --no-gui --source chocolatey 2>&1 | Out-String`: still prints output (nothing depended on the deleted files).
- [ ] **Step 5: Commit**: `git add -A` / `git commit -m "feat!: retire the v1 PowerShell implementation"`

---

## Done criteria for Plan 3

- Published single exe runs standalone (x64 verified locally; arm64 builds).
- Installer script builds per-arch with the same AppId, purging v1 files on upgrade.
- Release workflow: test gate → publish ×2 → installers ×2 → portable zips ×2 → SHA256SUMS → draft release on tag.
- README documents v2 with the migration table; no live references to the deleted v1 files remain.
- Tagging `v2.0.0` is a SEPARATE, human-confirmed step after the user verifies winget + a real upgrade in their session; not part of this plan's execution.
