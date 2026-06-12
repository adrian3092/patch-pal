# Changelog

All notable changes to this project will be documented in this file.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and
the project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2026-06-12

Complete rewrite: WinUpdateChecker is now a native C#/.NET 8 application.

### Added
- Fluent (Windows 11-style) GUI with sidebar navigation: Updates, All apps, History, Settings.
- Per-row and bulk updates with live in-place progress and result toasts.
- Persisted update history (%APPDATA%\WinUpdateChecker) with captured failure logs.
- Settings page: theme (System/Light/Dark), scan on launch, per-source toggles,
  system-components toggle.
- Self-contained single-exe builds for x64 and ARM64 — no PowerShell or .NET install needed.
- Parallel source queries — scans are faster than v1.

### Changed
- **Breaking:** CLI flags renamed (`-NoGui` → `--no-gui`, `-ExportCsv` → `--export-csv`,
  `-ExportHtml` → `--export-html`, `-Source` → `--source`,
  `-IncludeSystemComponents` → `--include-system-components`). Update scheduled tasks
  to invoke `WinUpdateChecker.exe` — see the README migration table.
- Failed package-manager queries now surface as visible warnings instead of being
  silently treated as "no updates".

### Removed
- **Breaking:** `UpdateChecker.ps1`, `Run.bat`, and `Run-Console.bat`. The installer
  removes them from existing installs on upgrade.

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
