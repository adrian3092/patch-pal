# WinUpdateChecker v2; C# / WPF Rewrite Design

**Date:** 2026-06-10
**Status:** Approved design, pending implementation plan
**Supersedes:** the single-file PowerShell implementation (`UpdateChecker.ps1`)

## Goal

Rewrite WinUpdateChecker as a modern, professional-looking native Windows application while preserving every capability of the v1 PowerShell script. The scan/match/upgrade logic is a 1:1 port of the proven v1 behavior, including its security fixes, not a redesign.

## Decisions (settled during brainstorming)

| Question | Decision |
|---|---|
| Keep pure PowerShell? | No; full rewrite approved |
| Stack | C# / .NET 8, WPF with the [WPF UI](https://github.com/lepoco/wpfui) Fluent library, MVVM via CommunityToolkit.Mvvm |
| Scope | Full parity: GUI + headless CLI in one exe; PS script retired |
| Layout | Sidebar navigation (Updates / All apps / History / Settings / About) |
| History feature | Included in v2.0 (persisted update log) |
| Publishing | Self-contained single-file exe, win-x64 + win-arm64 |
| Design language | Native Fluent (system accent, Segoe UI Variable, mica), light + dark following system; dark is the showcase default in screenshots |
| Version | v2.0.0 |

## Architecture

```
WinUpdateChecker.sln
├── src/WinUpdateChecker.Core/        # class library; all logic, zero UI references
│   ├── Scanning/   RegistryScanner   # HKLM + HKCU + WOW6432Node uninstall keys
│   ├── Sources/    IPackageSource    # → WingetSource, ScoopSource, ChocoSource
│   ├── Matching/   PackageMatcher    # exact → base-name → word-boundary; version compare
│   ├── Upgrading/  UpgradeRunner     # validates ids, dispatches to owning source CLI
│   ├── Export/     CsvExporter, HtmlExporter
│   └── History/    HistoryStore      # JSON at %APPDATA%\WinUpdateChecker\history.json
├── src/WinUpdateChecker.App/         # WPF + WPF UI; references Core
│   ├── Views/      MainWindow, UpdatesPage, AllAppsPage, HistoryPage, SettingsPage, AboutDialog
│   ├── ViewModels/ one per view (CommunityToolkit.Mvvm)
│   └── Cli/        CliRunner         # --no-gui etc.; headless path, no WPF objects created
└── tests/WinUpdateChecker.Core.Tests/  # xUnit; ports all Pester cases from tests/Merge.Tests.ps1
```

- `IPackageSource` contract: `IsInstalled()`, `ListOutdatedAsync(ct)`, `UpgradeAsync(packageId, progress, ct)`. Each source parses its own CLI output; parsers take raw text so they are unit-testable with fixtures.
- Process invocation goes through a single `ProcessRunner` wrapper (captures stdout/stderr, timeout, cancellation) so no source shells out ad hoc.
- The ported v1 behaviors that MUST survive verbatim: three-tier name matching, version comparison rules, phantom VC++ row suppression (6610a06), package-id validation before upgrade (b95dca8), per-source upgrade dispatch, winget/choco elevation via normal UAC.

## UI design

**Window:** Fluent window (WPF UI `FluentWindow`), mica backdrop where available, sidebar `NavigationView` left, content right. Follows system light/dark and system accent color.

**Updates page (landing):**
- Header: "Updates available" + relative "Last scanned X min ago"; right side: ghost `Scan` button (becomes `Cancel` during a scan) and primary `Update all (N)`.
- Selection bar: select-all checkbox, "N selected" label, `Update selected` button, filter text box (live substring filter on name/id/source).
- Rows: checkbox, app glyph, name, `installed → available` (available version emphasized), source badge (winget/scoop/choco), per-row `Update` button.
- During upgrade a row's button is replaced in place by a slim progress bar + status text; finished rows show "✓ Updated" (success color) or "✕ Failed; view log" (opens captured CLI output). No console windows are ever shown. A toast summarizes the batch when it completes.
- Empty state when no updates: friendly "Everything is up to date" with last-scan time and a Scan button.

**All apps page:** same row style, every detected program, including "up to date" and "unknown" statuses; same filter box. Equivalent to v1 grid with "Only show updates" off.

**History page:** entries grouped by date; each row: time, app, old → new version, source, success/failure. Failure rows expand to show the captured CLI log. `Clear history` button with confirmation.

**Settings page:**
- Theme: System / Light / Dark
- Scan on launch (default on)
- Per-source enable toggles; a missing package manager shows "not installed" (disabled, not an error)
- Include Windows system components (default off; v1 `-IncludeSystemComponents`)
- Settings persist to `%APPDATA%\WinUpdateChecker\settings.json`

**About dialog:** version, architecture (kept from 40f53b8), GitHub link.

**Design-language rules (from ui-ux-pro-max review, adapted for desktop):**
- All colors defined once as semantic resource tokens (success, danger, accent, surface…): no hard-coded hex in views; both themes derive from the same tokens.
- Green = update-available/success semantics; red = failure; never color alone; pair with icon or text.
- Segoe UI Variable; Fluent Segoe icon font or bundled SVG for icons; no emoji as icons.
- Visible keyboard focus on every interactive control; full keyboard operability; grid rows expose UIA/screen-reader names.
- Transitions 150–300 ms, transform/opacity only, honor Windows reduced-motion setting.
- Buttons disable + show progress during async work; feedback within 100 ms of click.

## CLI mode

Same exe. `--no-gui`, `--export-csv`, or `--export-html` runs headless on the console; `--source` and `--include-system-components` are mode-independent and also apply when launching the GUI (matching v1 behavior). CLI mode scans and exports only; upgrades are GUI-only, as in v1. Exit code 0 = success, 1 = scan failure.

| v1 (PowerShell) | v2 |
|---|---|
| `-NoGui` | `--no-gui` |
| `-ExportCsv <path>` | `--export-csv <path>` |
| `-ExportHtml <path>` | `--export-html <path>` |
| `-Source winget,scoop` | `--source winget,scoop` |
| `-IncludeSystemComponents` | `--include-system-components` |

This table also goes in the README as the migration guide. Export flags imply headless. Console output of `--no-gui` mirrors the v1 table format.

## Engine behavior

- **Scan:** registry read + the enabled source queries run concurrently (`Task.WhenAll`); v1 ran them sequentially, so scans get faster. Scans are cancellable.
- **Merge:** ported matcher combines registry programs with source-reported outdated packages; unknown programs remain listed with "Up to date / unknown" status as in v1.
- **Upgrades:** sequential per source (winget cannot run concurrently with itself); independent sources may run in parallel. Stdout/stderr captured per item and stored with the History entry. After a batch, an automatic rescan refreshes the list.
- **HTML/CSV export:** ports the v1 report including light/dark-aware HTML.

## Error handling

- Missing package manager → source skipped, shown as "not installed" in Settings; never an error.
- Source query failure → inline warning banner on Updates page ("winget query failed; results may be incomplete"); other sources still merge.
- Upgrade failure → row marked failed, log stored in History, batch continues.
- Corrupt `settings.json` / `history.json` → file backed up (`*.bak`) and recreated with defaults; app never crashes on bad state files.
- CLI mode errors print to stderr and set exit code 1.

## Testing

- `Core.Tests` (xUnit) ports every case in `tests/Merge.Tests.ps1`: matching tiers, version comparison, phantom-VC++ suppression, id validation.
- Parser tests per source using captured fixture output (winget/scoop/choco text): no real package managers invoked in tests.
- `ProcessRunner` mocked behind an interface for source tests.
- Coverage target: 80% on Core (per repo standard). View-models testable; views verified manually with a screenshot pass (light + dark) before release.

## Distribution, CI, migration

- `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true` plus the same for `win-arm64`.
- `installer/setup.iss` updated to install the published exe; portable zip contains just the exe; `Run.bat` is removed since the exe is directly double-clickable.
- `.github/workflows/release.yml` updated: build both architectures, run tests, produce installer + portable zips + `SHA256SUMS.txt` on tag push. SignPath plan unchanged.
- `UpdateChecker.ps1`, `Run-Console.bat`, and the Pester tests are removed from the repo root once parity ships (history preserved in git). README rewritten around v2 with the flag-migration table.
- Changelog starts a v2.0.0 entry.

## Out of scope (v2.0)

- New package sources beyond winget/Scoop/Chocolatey (the `IPackageSource` seam makes them future work).
- Auto-update of WinUpdateChecker itself.
- Scheduling UI (users keep using Task Scheduler with CLI flags, as documented).
- Localization beyond English.
