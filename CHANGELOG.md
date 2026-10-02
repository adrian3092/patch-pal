# Changelog

All notable changes to this project will be documented in this file.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and
the project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- Headless runs (`--no-gui`, `--export-csv`, `--export-html`) use the Settings page's
  source and system-component choices, as the Updates page does. `--source` picks the
  sources instead, and `--include-system-components` includes system components either way.
- A failed update's message now appears under the app name, where it has the row's full
  width; it is cut short with "…" only in a narrow window. Hover over it for the whole
  message and the installer log.

### Fixed
- **winget updates no longer vanish on non-English Windows.** winget prints its update
  list in the Windows display language, and Patch Pal only recognized the English
  table, so it reported "Everything is up to date". The list is now read by its layout
  in any language, including Chinese, Japanese, and Korean, whose characters are two
  columns wide. That also fixes misread rows for app names in those scripts on English
  Windows. If winget's output still can't be read, the scan shows a warning instead.
- **Update all** is greyed out when there's nothing Patch Pal can update (for example,
  only Microsoft Edge, which updates itself, is listed) and while an update is running.
- The All apps page shows "Updates itself" for Microsoft Edge, as the Updates page does.
- Leaving the Updates page and coming back no longer empties the list and claims "Everything is
  up to date"; the page keeps its scan results and any update in progress.
- **Cancel** stops a running scan and the package manager it was waiting on. The Scan button
  turned into Cancel but stayed disabled.

## [2.0.0] - 2026-10-01

Complete rewrite and rename: WinUpdateChecker is now **Patch Pal**, a native C#/.NET 8 application.

### Added
- Fluent (Windows 11-style) GUI with sidebar navigation: Updates, All apps, History, Settings.
- Per-row and bulk updates with live in-place progress and result toasts.
- Persisted update history (%APPDATA%\PatchPal) with captured failure logs.
- Settings page: theme (System/Light/Dark), scan on launch, per-source toggles,
  system-components toggle.
- Self-contained single-exe builds for x64 and ARM64 — no PowerShell or .NET install needed.
- Parallel source queries — scans are faster than v1.
- Patch Pal band-aid icon on the exe, the title bar, and the installer.

### Changed
- **Breaking:** the application is renamed from WinUpdateChecker to Patch Pal
  (`PatchPal.exe`). Existing settings and update history in
  %APPDATA%\WinUpdateChecker are migrated automatically on first launch.
- **Breaking:** Patch Pal now requests administrator rights at launch (one UAC
  prompt) instead of prompting for every winget/Chocolatey upgrade. Upgrades run
  silently with output captured in History; no more flashing console windows.
  Scheduled tasks must be registered with "Run with highest privileges".
- **Breaking:** CLI flags renamed (`-NoGui` → `--no-gui`, `-ExportCsv` → `--export-csv`,
  `-ExportHtml` → `--export-html`, `-Source` → `--source`,
  `-IncludeSystemComponents` → `--include-system-components`). Update scheduled tasks
  to invoke `PatchPal.exe` — see the README migration table.
- Failed package-manager queries now surface as visible warnings instead of being
  silently treated as "no updates".
- v1's "Up to date / unknown" status is split in two: "Up to date" (a package manager
  confirms the program is current) and "Not tracked" (no package manager knows it).
  CSV and HTML reports use the new labels.

### Removed
- **Breaking:** `UpdateChecker.ps1`, `Run.bat`, and `Run-Console.bat`. The installer
  removes them from existing installs on upgrade.

### Fixed
- **Microsoft Edge no longer offers an update that always fails.** Windows installs
  Edge with Edge's own installer, but winget only has Edge as an MSI, so
  `winget upgrade` refuses it (`0x8A15008E`, install technology mismatch). Edge's
  row now reads "Updates itself" instead of showing an Update button, and Update all
  skips it — Edge Update keeps Edge current.
- An upgrade that winget refuses because of an install technology mismatch now
  explains why instead of showing a raw exit code.
- An upgrade that fails for lack of an internet connection (winget `0x8A150107`) no
  longer reports "No installer applicable"; that message now goes to the code that
  means it (`0x8A150010`).
- **.NET SDK updates no longer go missing.** The SDK registers `8.4.2226.x` in
  Programs and Features while winget calls the same install `8.0.422`, so comparing the
  registry number with winget's `8.0.425` hid the update. When winget names the exact
  installed program, Patch Pal now compares winget's own installed and available
  versions, including winget's "< X" (older than X) notation.

## [1.0.3] - 2026-06-04

### Security
- **Validate package ids before upgrading.** `Invoke-PackageUpgrade` now rejects
  any package id outside the safe identifier charset (`^[\w.+-]+$`) before
  launching an installer. This closes a command-injection path where a crafted
  package name parsed from `scoop status` output could have been interpolated
  into a child-shell command. Whitelist validation is applied uniformly to the
  winget, Scoop, and Chocolatey dispatch paths.

## [1.0.2] - 2026-06-04

### Fixed
- **Phantom update rows.** Name matching no longer collapses different product
  editions together, so Visual C++ 2013 (v12) and stale leftover redistributable
  entries are no longer falsely reported as updates to the 2015+ winget package.
- **Duplicate entries.** Multiple installed entries that map to the same package
  id now collapse into a single row (highest installed version wins).
- **Silent upgrade failures.** Upgrades now capture the winget exit code and
  report per-package success/failure (with a readable reason) instead of
  swallowing the result, so a failed or no-op upgrade is no longer invisible.
- **Stale in-app version.** `$script:Version` was still `1.0.0` on the 1.0.1
  release; the About dialog and title bar now reflect the real version.

### Added
- Version-aware update detection: a row is only flagged when the available
  version is genuinely newer than what's installed.
- `tests/Merge.Tests.ps1` — assertion suite for the matching/merge logic, with a
  dot-source guard so the script can be loaded for testing without launching the GUI.

## [1.0.1] - 2026-05-06

### Added
- Explicit ARM64 (Surface Pro X, Snapdragon Copilot+ PCs, Windows Dev Kit) support documentation in README.
- About dialog now displays OS architecture and PowerShell version alongside the app version.

### Notes
- No code-path changes for ARM64 — the tool was already architecture-agnostic. This release makes that explicit and surfaces it in the UI.
- Inno Setup config comments clarify that `x64compatible` covers both x64 and ARM64 install modes.

## [1.0.0] - 2026-05-06

Initial public release.

### Added
- Registry-based scan of installed programs (HKLM, HKCU, WOW6432Node).
- Update detection from **winget**, **Scoop**, and **Chocolatey**.
- WinForms GUI with sortable grid, filter box, and "Only show updates" toggle.
- One-click **Update Selected** and **Update All Available** actions.
- CSV and stand-alone HTML report export (light/dark theme aware).
- Console mode (`-NoGui`) and direct export modes (`-ExportCsv`, `-ExportHtml`).
- `-Source` flag to restrict the package managers queried.
- `-IncludeSystemComponents` flag to include hidden Windows components.
- About dialog with version and repo link, F5 refresh shortcut.

### Distribution
- Inno Setup installer (`installer/setup.iss`) producing `WinUpdateChecker-Setup-x.y.z.exe`.
- Portable zip artifact built alongside the installer.
- GitHub Actions release workflow (`.github/workflows/release.yml`) — tag push builds installer, zip, and `SHA256SUMS.txt`, then drafts a release.
- Authenticode signing helper (`tools/Sign-Script.ps1`).
- Signing documentation including SignPath OSS application steps (`docs/SIGNING.md`).
