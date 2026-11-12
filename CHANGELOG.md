# Changelog

All notable changes to this project will be documented in this file.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and
the project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.3.0] - 2026-11-12

### Security
- **Releases are code signed.** The installers and `PatchPal.exe` carry an Authenticode
  signature, so Windows shows the publisher's name instead of "Unknown publisher". SmartScreen
  may still warn about a download until the new signature has built up reputation.
- Patch Pal now runs on .NET 10, which Microsoft supports with security fixes until November
  2028. .NET 8 reached the end of its support on November 10, 2026, so flaws found in it from
  now on won't be fixed. The runtime ships inside the exe, so updating Patch Pal is all it takes.

### Changed
- Times and dates follow your Windows region format: 12-hour (6:17 PM) in the US, 24-hour
  (18:17) in most of Europe, and whichever you picked if you changed it in Windows Settings.
  They were always 24-hour before.
- The exe is about 20 MB smaller.

### Fixed
- **A failed update's reason stays on screen.** The Updates page rescans when a batch finishes,
  and the rescan wiped every failure message a moment after it appeared. A failed update now
  keeps its message after the rescan, until you retry it or a newer version is offered.
- **Failed updates on ARM PCs say why.** winget often has only an x64 installer for an app, and
  some apps refuse to install that on ARM (NordVPN and VeraCrypt, for example). When an update
  fails on an ARM PC, Patch Pal now checks whether winget has an ARM64 installer for the app;
  if it doesn't, the update says so and suggests updating from within the app or with the
  publisher's ARM64 installer. The installer's own error stays in the log.
- **Dark mode is readable.** Page titles and text were near-black on the dark background,
  the app lists on Updates and All apps sat on a white panel, and History's "View log" was
  black. All now follow the theme.
- Before the first scan (with scan on launch off), the Updates page no longer says "Everything
  is up to date"; it says no scan has run yet.
- On Settings, the description under "Include Windows system components" no longer runs
  into its switch.

## [2.2.0] - 2026-11-11

### Fixed
- **Update history can no longer be wiped.** If another program had `history.json` open
  when an update finished, Patch Pal read the history as empty and saved just the new entry
  over it. It now leaves the file alone. A save also replaces the file in one step, so a crash
  can't leave half a file. Two Patch Pal windows no longer overwrite each other's entries, and
  each corrupt file is kept as its own timestamped `.bak` instead of replacing the last one.
- The History page says so when it can't read the history, instead of "No updates applied yet".
- Changing a setting no longer resets the others when `settings.json` couldn't be read at
  startup. A change now applies to what the file holds, and when the file can't be read the
  change lasts until Patch Pal closes, without touching the file.
- Headless runs warn when `settings.json` is locked, as they already did when access was denied,
  instead of quietly using the defaults.
- **A History entry that can't be saved no longer closes Patch Pal.** The app used to crash
  when the batch ended, after skipping the rest of that package manager's updates. Now the
  batch finishes, and its summary says History couldn't be saved and why. A row whose update
  never reported back is marked failed instead of spinning forever.
- Other unexpected errors are written to `%APPDATA%\PatchPal\error.log` and shown in a
  message, and Patch Pal stays open instead of disappearing, unless the error keeps coming back.
- **Microsoft Store apps that winget lists can now be updated.** Patch Pal always asked
  winget's own catalog (`--source winget`), so every update listed under `msstore`, or under
  a source you added yourself, failed with "No installed package matched for upgrade". Each
  update now comes from the source winget listed it under.
- **Fewer phantom updates.** A version written with fewer parts no longer counts as older
  than the same version written with more (8.6 vs 8.6.0). Whole-number versions such as 121
  are now compared, instead of always counting as an update. An old installer's leftover
  registry entry no longer shows an update once the same product's current entry already
  has it, as happened with the Visual C++ redistributables.
- An app whose name begins like another app's no longer takes that app's update. Microsoft
  Edge WebView2 Runtime used to take over Microsoft Edge's row and push Edge off the list.
  When two registry entries share one update, both now stay on All apps and in exports; the
  extra one is marked "Duplicate entry" instead of disappearing.
- Switching off every package source in Settings now really switches them off. Patch Pal used
  to query every package manager anyway. The Updates page and headless runs now say the sources are switched off,
  instead of "no package manager detected".
- **"Launch Patch Pal" at the end of setup works.** It failed with "CreateProcess failed;
  code 740" because Patch Pal needs administrator rights; it now shows the usual UAC prompt.

### Security
Patch Pal runs as administrator, so a program running under your normal account could get admin
rights by changing something Patch Pal loads or runs. Everything it loads or runs while it has
admin rights now comes from a folder only an administrator can change:
- WPF's native DLLs ship beside `PatchPal.exe` instead of unpacking into your Temp folder.
- **Setup always installs for all users, into Program Files.** The "just for me" option is gone.
  An old per-user copy is removed with its own uninstaller run with your normal rights, never as
  admin; settings and history are kept. **If a scheduled task or shortcut of yours points at
  `%LOCALAPPDATA%\Programs\PatchPal\PatchPal.exe`, point it at
  `%ProgramFiles%\PatchPal\PatchPal.exe` instead.**
- Scoop runs with your normal rights. Scoop apps installed for all users (`scoop install -g`)
  must be updated from an admin prompt.
- Chocolatey is used only from its own install folder, and only when only an administrator can
  change it; a `choco` found elsewhere on PATH is ignored.
- Windows tools (PowerShell, cmd, explorer and others) run from System32 by full path, DLLs load
  only from system folders, and child processes don't inherit .NET's startup-hook and profiler
  variables.
- The About box's **Open GitHub** link opens your browser with your normal rights, not as admin.

Accepted limits: winget is still found through PATH, the same way an admin prompt finds it, and
the .NET runtime reads its startup variables before Patch Pal's own code runs. Chocolatey's
package scripts run in PowerShell as admin and may load modules from your Documents folder.

## [2.1.0] - 2026-10-02

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
