# WinUpdateChecker

A native Windows 11-style app that scans your installed programs and tells you which ones have updates available.

It reads the same uninstall registry keys that **Programs and Features** uses, then cross-references them against three of the most popular Windows package managers — **winget**, **Scoop**, and **Chocolatey** — to find updates. One-click updates for any program a package manager can update, with a persisted update history so you can see what changed and when.

Built in C# on .NET 8 with a Fluent (WPF) interface. Light and dark themes. Zero telemetry. Ships as a single self-contained exe — no .NET install needed.

> **v2.0 is a complete rewrite.** v1 was a PowerShell + WinForms script; v2 is a native application. See the [CHANGELOG](CHANGELOG.md) and the [Migrating from v1.x](#migrating-from-v1x) section below.

---

## Features

- Fluent (Windows 11-style) GUI with sidebar navigation: **Updates**, **All apps**, **History**, **Settings**.
- Scans installed programs from the Windows registry (HKLM + HKCU + WOW6432Node).
- Queries every package manager you have installed (winget / Scoop / Chocolatey) — in parallel, so scans are fast.
- Per-row and bulk updates with live in-place progress.
- Persisted update history with captured failure logs for troubleshooting.
- Export to **CSV** or a stand-alone **HTML report** via the CLI.
- Headless CLI mode for scheduled tasks (`--no-gui`, `--export-csv`, `--export-html`).
- Light / dark / system theme.
- x64 and ARM64 builds.

## Screenshots

> Place screenshots in `docs/screenshots/` and reference them here.
>
> ![GUI screenshot placeholder](docs/screenshots/gui.png)
> ![HTML report placeholder](docs/screenshots/report.png)

## Requirements

- Windows 10 1809+ or Windows 11 — **x64 or ARM64** (Snapdragon Copilot+ PCs, Windows Dev Kit, etc.).
- At least one of these for update detection:
  - [winget](https://learn.microsoft.com/windows/package-manager/winget/) — pre-installed on modern Windows; otherwise install **App Installer** from the Microsoft Store.
  - [Scoop](https://scoop.sh/) — optional.
  - [Chocolatey](https://chocolatey.org/) — optional.
- **No .NET runtime needed** — the exe is fully self-contained.

## Install

Pick whichever you prefer — both ship from the same GitHub release page, per architecture.

### Installer (recommended for most users)

Download the installer for your architecture from the [latest release](https://github.com/adrian3092/win-update-checker/releases/latest):

- `WinUpdateChecker-Setup-x.y.z-x64.exe` — Intel/AMD PCs
- `WinUpdateChecker-Setup-x.y.z-arm64.exe` — ARM PCs (Snapdragon, etc.)

Run it, click through the wizard, launch from the Start menu. Standard Add-or-Remove-Programs uninstall. Installing v2 over an existing v1 install upgrades it in place.

### Portable zip

1. Download `WinUpdateChecker-portable-x.y.z-x64.zip` (or `-arm64.zip`).
2. Right-click the downloaded zip → **Properties** → tick **Unblock** → OK.
3. Extract anywhere.
4. Run **`WinUpdateChecker.exe`**.

That's it. The first scan takes 5–15 seconds.

### From source

```powershell
git clone https://github.com/adrian3092/win-update-checker.git
cd win-update-checker
dotnet run --project src/WinUpdateChecker.App
```

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

## Usage

### GUI

Just run `WinUpdateChecker.exe`. Scan, review, and update from the **Updates** page; browse everything installed on **All apps**; review past updates (including failure logs) on **History**.

### CLI

Run the same exe with flags for headless use:

| Flag | Description |
|---|---|
| `--no-gui` | Print available updates to the console and exit |
| `--export-csv <path>` | Write a CSV report and exit |
| `--export-html <path>` | Write a stand-alone HTML report and exit |
| `--source <list>` | Restrict sources: `winget,scoop,chocolatey` |
| `--include-system-components` | Include Windows components and hotfixes |

```powershell
# Print updates to the console
WinUpdateChecker.exe --no-gui

# Generate a stand-alone HTML report
WinUpdateChecker.exe --export-html report.html

# Generate a CSV (handy for scheduled tasks)
WinUpdateChecker.exe --export-csv "$env:USERPROFILE\Desktop\updates.csv"

# Restrict to specific package managers
WinUpdateChecker.exe --no-gui --source winget
WinUpdateChecker.exe --no-gui --source scoop,chocolatey

# Include Windows components and hotfixes
WinUpdateChecker.exe --no-gui --include-system-components
```

Exit code `0` on success, `1` on error. The CLI is scan/export only — running upgrades is done from the GUI.

## Migrating from v1.x

v2 removes `UpdateChecker.ps1`, `Run.bat`, and `Run-Console.bat` — the installer deletes them from an existing install when you upgrade. Anything that invoked the script (scheduled tasks, shortcuts, wrapper scripts) must be updated to call the exe:

| v1 (PowerShell) | v2 |
|---|---|
| `.\Run.bat` | `WinUpdateChecker.exe` |
| `-NoGui` | `--no-gui` |
| `-ExportCsv <path>` | `--export-csv <path>` |
| `-ExportHtml <path>` | `--export-html <path>` |
| `-Source winget,scoop` | `--source winget,scoop` |
| `-IncludeSystemComponents` | `--include-system-components` |

## Schedule a daily check

Run this once in an admin PowerShell window. It creates a Task Scheduler job that writes a fresh HTML report at 9 a.m. (adjust the paths to your install location):

```powershell
$action  = New-ScheduledTaskAction -Execute "$env:LOCALAPPDATA\Programs\WinUpdateChecker\WinUpdateChecker.exe" `
    -Argument '--export-html "C:\Tools\WinUpdateChecker\latest.html"'
$trigger = New-ScheduledTaskTrigger -Daily -At 9am
Register-ScheduledTask -TaskName 'WinUpdateChecker Daily' -Action $action -Trigger $trigger
```

## How it works

```
+---------------------------+
|  Registry Uninstall keys  |  <-- HKLM, HKCU, WOW6432Node
+-------------+-------------+
              |
              v
+---------------------------+      +---------------------+
|  Installed program list   |      |  winget upgrade     |
+-------------+-------------+      |  scoop status       |
              |                    |  choco outdated -r  |
              |                    +----------+----------+
              |                               |  (queried in parallel)
              +---------+---------+-----------+
                        |         |
                        v         v
              +-------------------------+
              |  Name match + version   |  <-- exact, then base-name,
              |  comparison             |      then word-boundary
              +-----------+-------------+
                          |
                          v
              +-------------------------+
              |     GUI  /  CLI         |
              +-------------------------+
```

The **Source** column tells you which manager owns each update; updating dispatches to the correct CLI (`winget upgrade`, `scoop update`, `choco upgrade`). winget and Chocolatey upgrades trigger a UAC prompt; Scoop runs as the current user.

## FAQ

**Does it modify anything on its own?**
No. It only reads the registry and queries package managers. Updates only run when you explicitly click an Update button in the GUI.

**Why does Windows say "Windows protected your PC"?**
SmartScreen warns on unsigned downloads it hasn't seen before. Right-click the zip → Properties → Unblock before extracting, or click **More info → Run anyway** on the installer. Verify the SHA-256 hash first (see below).

**Why is the download ~80 MB?**
The exe bundles the entire .NET runtime so it runs on any machine with zero prerequisites — no .NET install, no PowerShell version requirements.

**It says my program is "Up to date / unknown" but I know there's an update.**
That program isn't in any of the package-manager catalogs. WinUpdateChecker can only report on what winget/Scoop/Chocolatey know about. For full coverage, install winget at minimum.

**Does it support PortableApps / Microsoft Store / Steam / etc.?**
Microsoft Store apps appear if winget can see them (it usually can). Steam and other store-managed apps are reported as installed but updates are managed by their own clients.

**Can I run it without admin?**
Yes — for scanning. Some upgrades (winget, choco) request elevation when invoked.

## Verify a release

Each GitHub release includes a `SHA256SUMS.txt`. Verify a downloaded artifact:

```powershell
Get-FileHash .\WinUpdateChecker-Setup-2.0.0-x64.exe -Algorithm SHA256
```

Compare against the published hash before running. Once the project is enrolled in [SignPath OSS signing](docs/SIGNING.md), installer downloads will also carry a verifiable Authenticode signature — check the file's **Properties → Digital Signatures** tab.

## Building releases

```powershell
# Publish the self-contained exe (per architecture)
dotnet publish src/WinUpdateChecker.App -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o dist/publish-x64

# Build the installer (requires Inno Setup 6 installed)
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" `
  /DMyAppVersion=2.0.0 /DMyAppArch=x64 /DMySourceDir=..\dist\publish-x64 `
  installer\setup.iss
```

Swap `win-x64` / `x64` for `win-arm64` / `arm64` to build the ARM64 artifacts.

CI builds run automatically on tag push: `git tag v2.0.1 && git push --tags`. See [`.github/workflows/release.yml`](.github/workflows/release.yml) and [`docs/SIGNING.md`](docs/SIGNING.md) for details.

## Contributing

PRs welcome. Please:

1. Open an issue first for non-trivial changes.
2. Use the .NET 8 SDK; `dotnet test` must pass.
3. Keep `WinUpdateChecker.Core` UI-free — all WPF code lives in `WinUpdateChecker.App`.

## License

[MIT](LICENSE).
