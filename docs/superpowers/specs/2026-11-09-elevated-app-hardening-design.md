# Patch Pal: Hardening the App That Runs as Admin

**Date:** 2026-11-09
**Status:** Draft, waiting for the owner's approval
**Source:** finding #1 of the 2026-11-09 end-to-end review ("the elevated app loads code from user-writable folders")

## Why

Patch Pal asks for administrator rights every time it starts, because `app.manifest` is set to `requireAdministrator`. The owner wants to keep that one prompt. They don't want backlog #5, which would ask for admin once per update instead.

While Patch Pal has admin rights, some of the code it loads or runs can be changed by any program running with the user's normal rights. Such a program never needs a prompt of its own:

1. It changes one of those files or settings.
2. It waits for the user to approve Patch Pal's usual prompt.
3. Its code then runs as admin.

This work closes those paths.

**The rule:** everything Patch Pal loads or runs while it has admin rights comes from a folder only an admin can change. The non-goals list the exceptions the owner accepted.

## What breaks the rule today

| # | What | How it's loaded or run | Why a program with normal rights can change it |
|---|---|---|---|
| 1 | WPF's native DLLs: `D3DCompiler_47_cor3`, `PenImc_cor3`, `PresentationNative_cor3`, `vcruntime140_cor3`, `wpfgfx_cor3` | The single-file exe unpacks them to `%TEMP%\.net\PatchPal\<bundle id>\`, because `release.yml` publishes with `IncludeNativeLibrariesForSelfExtract=true`. | Temp belongs to the user. The .NET 8 host re-extracts only missing files and never checks the ones already there (confirmed in the dotnet/runtime release/8.0 source), so a replaced DLL loads at the next launch. On the owner's PC, Temp can also be changed by the `CodexSandboxUsers` group. |
| 2 | `PatchPal.exe` from a "for me only" install | `installer/setup.iss` has `PrivilegesRequired=lowest` and `PrivilegesRequiredOverridesAllowed=dialog`. Setup therefore offers "Install for me only" and puts the exe in `%LOCALAPPDATA%\Programs\PatchPal`. | That folder belongs to the user. The README's scheduled-task example points at this path with `-RunLevel Highest`, which runs it as admin with no prompt at all. |
| 3 | `PatchPal.exe` from the portable zip | It runs from wherever the zip is extracted, usually Downloads. | Same as 2. |
| 4 | Scoop | `scoop status` goes through the cmd fallback, which finds Scoop's `.cmd` shim. Updates run `powershell -NoProfile -Command "scoop update <id>"`. | Scoop and its scripts live in the user's profile (`C:\Users\<user>\scoop`). The comment in `ScoopSource` saying Scoop runs without elevation is out of date. |
| 5 | Chocolatey | `where.exe choco` checks whether it's installed, then `choco` is started by name. | The search ends with the user's own PATH folders. When Chocolatey isn't installed, a `choco.exe` or `choco.cmd` dropped into any of them is detected and run as admin at every scan. Chocolatey isn't installed on the owner's PC, and `C:\Users\<user>\scoop\shims` is one of those folders. |
| 6 | `where.exe`, `cmd.exe`, `powershell` | Started by name. | Windows looks in the app's folder and the current folder before System32. That matters for the portable zip and for whatever folder Patch Pal is started from. |
| 7 | The browser, from About → **Open GitHub** | `Process.Start` with `UseShellExecute = true` (`MainWindow.xaml.cs`). | From an admin process, this starts the URL's handler as admin. Windows looks the handler up in a registry branch the user's normal account can change: in the merged view, `HKCU\Software\Classes` overrides HKLM. |
| 8 | Any DLL that isn't in the app's folder or the Windows folders | Windows' default DLL search. | After those folders, Windows searches the current folder and every PATH folder, including the user's. |
| 9 | .NET's own startup hooks and profiler | The runtime reads environment variables and loads code they name before `Main` runs. | A normal account can set those variables with no prompt. Because the code runs before `Main`, the app can't defend itself from inside. Every single-file .NET app that runs as admin has this. Confirmed by the reviewer on a build made the way Patch Pal is. |

## Goals

1. Patch Pal loads nothing from Temp. The five DLLs ship beside `PatchPal.exe`.
2. Setup installs for all users only, in Program Files, and replaces an older "for me only" copy.
3. Scoop runs with the user's normal rights, for both the scan and updates.
4. Patch Pal looks for Chocolatey only in the folder Chocolatey's installer chose.
5. Windows' own tools start by their full System32 path. DLLs load only from Patch Pal's folder and System32.
6. The About box's link opens the browser with normal rights.
7. The README, the CHANGELOG and `docs/SIGNING.md` describe the new behavior.

## Non-goals

- **Asking for admin once per update** (backlog #5). The owner keeps the one prompt at launch.
- **Row 9: code the .NET runtime loads before `Main`.** The app can't defend against this from inside, because it runs first. Patch Pal does the cheap mitigations (part 5 and "My calls") and the Security note names this as an accepted limit, on the same reasoning as winget: every admin-launched single-file .NET app has it. A separate launcher could close it fully; the owner chose not to build one this round.
- **The data files and the exports.** Patch Pal writes them as admin in folders a normal account controls. The fix reuses the Scoop no-admin launch and lands in a follow-up round; see the note at the end.
- **winget itself.** Patch Pal starts `winget` the same way an admin prompt does: Windows finds it through PATH.
  - Both winget's alias folder (`%LOCALAPPDATA%\Microsoft\WindowsApps`) and the user PATH folders ahead of it can be changed with normal rights. On the owner's PC, four user folders come first, including `scoop\shims`.
  - Every admin who types `winget` has the same exposure.
  - Microsoft doesn't support running winget's packaged exe directly, so winget stays as it is. The owner agreed.
- **What winget and Chocolatey do once they're running**, such as where they download installers.
- **Making the portable zip safe in a user folder.** That isn't possible while it runs as admin. The owner chose to keep publishing it.
- **Cleaning up old `%TEMP%\.net\PatchPal` folders.** Nothing loads them after this change.
- **A standard user's "for me only" copy when a different admin account approves setup.** Setup then runs as that admin and can't see the other user's install. The README says how to remove it.

## Anti-goals

Each of these counts as a failure even if everything else works.

- Running Scoop, or anything else, as admin because the normal-rights launch failed. If that launch fails, Scoop fails visibly.
- Breaking the in-place upgrade of an all-users install, which is the owner's own setup. Settings and history in `%APPDATA%\PatchPal` must survive.
- Losing a "for me only" user's settings or history when their copy is replaced.
- Adding a UAC prompt. The only prompts remain setup's own and Patch Pal's one at launch.
- Running the old "for me only" uninstaller with admin rights. It sits in a folder the user can change, so doing that would reopen the same hole.
- Breaking the GUI test recipe, which runs the app with `__COMPAT_LAYER=RunAsInvoker` and puts a fake `winget.cmd` first on PATH. The cmd fallback finds that file, so keep the fallback for winget.

## Constraints

- .NET 8 WPF, self-contained, for x64 and arm64. `PatchPal.exe` stays a single-file bundle; only the native DLLs move out of it.
- Inno Setup 6. ISCC isn't installed on the owner's PC, so installers are built by `release.yml`. It also has `workflow_dispatch` for a test build before tagging.
- Builds aren't signed yet. SignPath will sign `PatchPal.exe` and the installers. Microsoft has already signed the five DLLs.
- Code changes are written test-first.
- Commits go straight to main. Nothing is pushed or released until the owner asks, and releases are on hold.
- Checks on the owner's PC touch the real install and the real `%APPDATA%\PatchPal`. Back that folder up and restore it afterwards, and ask before installing or uninstalling anything there.

## Decided by the owner

- Keep the UAC prompt at every launch.
- Setup installs for all users only.
- Keep publishing the portable zip next to the installer.
- Run Scoop without admin.
- Leave winget alone.
- Ship the DLLs beside the exe.
- Row 9 (code the runtime loads before `Main`): accept it and document it, the same as winget. No separate launcher. Still do the cheap mitigations (startup hooks off, clean variables for elevated children).
- The data-file and export-write hardening is a **follow-up round**, after the Scoop no-admin launch is built; it isn't in this round.
- Checks that need admin run on the owner's PC in attended sessions. Nothing is pushed until the owner asks; no automated CI test workflow.

## My calls (the owner can override them)

- **Portable zip.** The README says to extract the whole folder somewhere only an admin can change, such as a folder under `C:\Program Files`, or to use the installer instead. It also says why. The app itself shows no warning.
- **Old "for me only" copy.** Before installing, setup runs the old copy's uninstaller as the original user, with Inno's `ExecAsOriginalUser`. If that fails, setup still installs. At the end it says the old copy is still there.
- **Scheduled tasks** pointing at the old per-user path aren't changed automatically. The CHANGELOG and README say to repoint them. The owner's PC has none.
- **Open GitHub** stays. It goes through the desktop shell, so the browser starts with normal rights.
- **Scoop apps installed for all users** (`scoop install -g`) need admin to update. Patch Pal won't run Scoop as admin for them. Their update fails with a message telling the user to run `scoop update -g <app>` from an admin prompt. The builder checks how such apps appear in `scoop status`.
- **The DLL search** is locked with `SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS)` as the first statement of `Program.Main`.
- **Startup hooks (row 9)** are turned off at build time with `StartupHookSupport=false`. This costs nothing and closes one of the two variables.
- **Child processes Patch Pal starts as admin** (choco, and anything else) get a cleaned set of environment variables, with the .NET and .NET-Framework loader variables removed, so Patch Pal can't be used to hand those on to an elevated child. The profiler variable on the app's own process (the other half of row 9) can't be closed from inside the app; the owner chose to accept that rather than add a launcher (see "Decided by the owner").
- **Checks that need admin** run on the owner's PC with the owner at the keyboard, approving each prompt, in a session they attend; nothing is pushed for CI unless the owner picks the automated path in the testing question below. Installer builds use the existing `workflow_dispatch`, which doesn't need a tag.

## Where the code is

- Launching processes: `src/PatchPal.Core/Sources/ProcessRunner.cs` and `IProcessRunner.cs`.
- Package managers: `ScoopSource.cs`, `ChocoSource.cs` and `WingetSource.cs` in the same folder.
- Entry point: `src/PatchPal.App/Program.cs`.
- The About box: `src/PatchPal.App/MainWindow.xaml.cs`.
- Packaging: `.github/workflows/release.yml` and `installer/setup.iss`.
- Docs: `README.md`, `CHANGELOG.md` and `docs/SIGNING.md`.

## The parts

### 1. DLLs beside the exe

- In `release.yml`, drop `-p:IncludeNativeLibrariesForSelfExtract=true`. The publish folder then holds `PatchPal.exe` and the five `*_cor3.dll` files.
- Add a CI step that fails when the publish folder holds anything other than the expected files. That way a new native DLL can't silently go missing from setup or the zip.
- The `[Files]` section of `setup.iss` and the zip step both copy the DLLs along with the exe.
- **Done when** two things hold after installing, launching and scanning:
  - every module in the PatchPal process is under `C:\Program Files\PatchPal` or `C:\Windows`;
  - the new build creates no `%TEMP%\.net\PatchPal` folder.

### 2. Setup for all users only

- Set `PrivilegesRequired=admin` and remove `PrivilegesRequiredOverridesAllowed`. Then `{autopf}`, `{autoprograms}` and `{autodesktop}` mean Program Files, the all-users Start menu and the Public desktop.
- The old copy's key is `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\{60cbe8cd-e316-4bc8-9a92-96305ec2c7d2}_is1`.
  - v1 used the same AppId, so v1 "for me only" installs are covered too.
  - Run its `UninstallString` with `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART` through `ExecAsOriginalUser`. Wait for it to finish, then install.
  - The uninstaller leaves `%APPDATA%\PatchPal` alone, because `setup.iss` has no `[UninstallDelete]` section.
- **Done when** these four cases work and keep settings and history:
  1. a fresh install;
  2. an upgrade over a 2.1.0 all-users install;
  3. an upgrade over a 2.1.0 "for me only" install;
  4. an uninstall.

  Testing them needs a Windows machine where installing is fine.

### 3. Scoop with normal rights

- Add a runner method that starts a process with the user's normal rights.
  - It returns the same `ProcessResult` as `RunAsync` and handles cancel the same way.
  - `ScoopSource` uses it for both `status` and `update`.
  - `CommandExists("scoop")` can stay as it is, because `where.exe` only reads.
- **Spike the mechanism first.**
  - **Leading candidate:** a restricted copy of Patch Pal's own token, passed to `CreateProcessAsUser` with redirected pipes. In the copy:
    - the Administrators group, and the "local account and member of Administrators" group, are deny-only;
    - admin privileges are removed;
    - integrity is lowered to Medium.

    It works without a desktop, which covers scheduled tasks and `--no-gui`. It also runs as the same account Patch Pal runs as, which matters when a different admin approved the prompt.
  - **Fallback candidate:** the desktop shell's token, used with `CreateProcessWithTokenW`. It needs Explorer running, and it runs as whoever owns the desktop.
  - **Not a candidate:** the non-admin token Windows links to the admin one. As far as I know, it can't start processes without SeTcbPrivilege.
- The spike must show five things:
  1. `whoami /all`, started through the new launcher, shows the user's normal token: Medium Mandatory Level, Administrators as "Group used for deny only", and the same privileges a normal prompt has.
  2. Files Scoop writes are owned by the user and can be changed by them.
  3. Output, the exit code, and cancel (killing the whole process tree) all work.
  4. `scoop status` and `scoop update` work on the owner's PC. Scoop is installed there but switched off in Settings, so switch it on for the test and off again afterwards.
  5. `scoop update` starts System32's `powershell.exe` by its full path, under the normal-rights token.
- If Patch Pal can't start a process with normal rights:
  - the scan shows a warning: "Couldn't start Scoop without admin rights, so its updates weren't checked";
  - an update fails with a clear message.
- Rewrite the out-of-date comment in `ScoopSource`.

### 4. Chocolatey from its own folder

- Read the machine-wide `ChocolateyInstall` value under `HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment`. Don't read it from the process environment, where a user variable with the same name overrides the machine one.
- If the value is missing, use `<ProgramData>\chocolatey`. Get ProgramData from the Known Folder API (`Environment.GetFolderPath(CommonApplicationData)`), not from `%ProgramData%`.
- Chocolatey counts as installed only when `<that folder>\bin\choco.exe` exists **and** that folder is one a normal account can't change. On the owner's PC, `C:\ProgramData` lets normal users create new subfolders, and `C:\ProgramData\chocolatey` doesn't exist, so "the file is there" on its own would let a dropped `choco.exe` run as admin — the same hole as row 5 in another folder. So check the folder's owner and rights, not just that the file exists, before trusting it. If the folder can be changed by a normal account, treat Chocolatey as not installed, with no row and no warning (the same as a PC without Chocolatey).
- Put the registry read, the folder-trust check and the file check behind a small seam, so tests can cover them.

### 5. Windows' tools and the DLL search

- Start `where.exe`, `cmd.exe` and `powershell.exe` from `Environment.SystemDirectory`. PowerShell is at `WindowsPowerShell\v1.0\powershell.exe` under it. Start `explorer.exe` from the Windows folder.
- Keep the cmd fallback for `winget`, now through System32's `cmd.exe`.
- Call `SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS)` before anything else in `Main`, including `CliOptions.Parse`. The module check from part 1 covers it.

### 6. The About link

- Open `AppInfo.RepoUrl` through the desktop shell with `explorer.exe "<url>"`. If that handoff turns out to be unreliable, use `IShellDispatch2.ShellExecute` instead.
- **Done when** the browser process runs at Medium integrity.

### 7. Docs

- In the README:
  - Install: setup asks for admin and installs for all users.
  - Portable zip: keep the whole folder together, and where to put it and why.
  - Scheduled task: use `"$env:ProgramFiles\PatchPal\PatchPal.exe"`, and say why the task must point at a copy only an admin can change.
- In the CHANGELOG's `[Unreleased]`: a Security section, plus a note for "for me only" users who have scheduled tasks.
- In `docs/SIGNING.md`: Microsoft has already signed the DLLs, so only `PatchPal.exe` and the installers get signed.

## How we'll know it's done

- [ ] All tests pass with `-warnaserror`. That includes new tests for the Chocolatey lookup, the full tool paths, and which launcher each source uses.
- [ ] The module check from part 1 passes on an installed build.
- [ ] The setup checks from part 2 pass.
- [ ] The spike checks from part 3 pass. When the normal-rights launch is forced to fail, Scoop shows a warning instead of running as admin.
- [ ] With Chocolatey not installed and a fake `choco.cmd` in a user PATH folder, Patch Pal neither lists nor runs Chocolatey.
- [ ] A fake `where.exe`, `cmd.exe` or `powershell.exe` in Patch Pal's folder or its working folder never runs.
- [ ] Open GitHub starts the browser at Medium integrity.
- [ ] The portable zip holds `PatchPal.exe`, the five DLLs and the docs, and it runs when extracted.

## The follow-up round (not this round)

The owner chose to harden Patch Pal's own writes in a later round, after the Scoop no-admin launch is built, because the clean fix reuses it.

With admin rights, Patch Pal writes, renames and deletes files in `%APPDATA%\PatchPal` (`history.json`, `settings.json`, `error.log`, and their `.tmp`/`.bak`/`.old` copies), and `--export-csv` / `--export-html` write wherever they're told — which for the README's scheduled task is a folder the user made. All of these run as admin in folders a normal account controls, so a normal-rights program could arrange for one of those admin writes to land on a file it shouldn't be able to touch.

The planned fix routes every write path — the exports and the crash logger included — through the same normal-rights launch part 3 builds for Scoop. (Moving the data under a machine-only folder was the alternative, but it needs a migration that keeps settings and history, and it would stop no-admin runs such as the test recipe from saving.) This is recorded here so the Scoop launcher in part 3 is built in a way the follow-up can reuse.
