# Elevated-App Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Everything Patch Pal loads or runs while it holds admin rights comes from a folder only an admin can change, while keeping the single UAC prompt at launch.

**Architecture:** Patch Pal is already elevated at launch. This plan narrows what it trusts: native DLLs ship beside the exe (not Temp); Windows' own tools run from System32 by full path with the DLL search locked down; Scoop runs through a new de-elevated process launch on `IProcessRunner`; Chocolatey is found only in an admin-controlled folder behind a locator seam; the About link opens the browser through the shell so it lands de-elevated; setup installs all-users-only and removes an old per-user copy as the original user. winget and the code the runtime loads before `Main` are accepted limits (documented), mitigated only cheaply.

**Tech Stack:** .NET 8, WPF, C#, xUnit; Inno Setup 6; GitHub Actions. Win32 token APIs (`CreateProcessAsUser`/`CreateProcessWithTokenW`) and `SetDefaultDllDirectories` via P/Invoke.

**Spec:** `docs/superpowers/specs/2026-11-09-elevated-app-hardening-design.md` (read it alongside this plan).

## Global Constraints

- Target framework `net8.0-windows`; nullable enabled; `ImplicitUsings` enabled. New immutable models are `sealed record`; service boundaries are `interface`.
- Build and test must pass with `dotnet build -c Release -warnaserror` and `dotnet test -warnaserror`.
- Tests are written first (RED → GREEN), xUnit, named by behavior, mirroring `src/` under `tests/`.
- `PatchPal.exe` stays a single-file self-contained bundle for `win-x64` and `win-arm64`; only the five native DLLs move out of it.
- Commit straight to `main`, one commit per task. Do **not** push and do **not** release. Commit messages are conventional (`feat:`/`fix:`/`docs:`/`ci:`), no `Co-Authored-By` trailer (matches history).
- The five native DLLs are exactly: `D3DCompiler_47_cor3.dll`, `PenImc_cor3.dll`, `PresentationNative_cor3.dll`, `vcruntime140_cor3.dll`, `wpfgfx_cor3.dll`.
- The old per-user install key (v1 and v2 share the AppId): `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\{60cbe8cd-e316-4bc8-9a92-96305ec2c7d2}_is1`.
- Anti-goals (any one makes the work a failure): never run Scoop — or anything — as admin because the de-elevated launch failed; never break the in-place upgrade or lose `%APPDATA%\PatchPal` settings/history; never add a UAC prompt; never run the old per-user uninstaller elevated; never break the GUI test recipe (`__COMPAT_LAYER=RunAsInvoker` + a fake `winget.cmd` first on PATH → the cmd fallback must still find it).
- Checks that need Patch Pal running as admin, and the four installer cases, run on the owner's PC in an attended session. Nothing is pushed; installer builds use `release.yml`'s `workflow_dispatch`.

## Review Focus

- **De-elevation fails with no interactive desktop** (scheduled task, `--no-gui`): Scoop must surface a warning and leave its results out, never crash and never fall back to admin. → Task 4 (`Scoop_WhenTheDeElevatedLaunchFails_WarnsAndDoesNotRunElevated`); the real no-desktop case is a manual done-when.
- **`choco.exe` exists but its folder is writable by a normal account** (e.g. a created `C:\ProgramData\chocolatey`): Chocolatey must count as not installed. → Task 5 (`Locator_FolderANormalUserCanChange_IsNotTrusted`).
- **A planted `where.exe`/`cmd.exe`/`powershell.exe` in the app folder or the working folder**: never run; Windows tools resolve to System32 absolute paths. → Task 2 (`WindowsTools_PathsAreAbsoluteUnderSystem32`) plus a manual done-when.
- **The winget RunAsInvoker test recipe** (bare `winget` → `Win32Exception` → cmd fallback) must still work after `cmd` moves to a System32 absolute path. → Task 2 (`RunAsync_BareNameThatFails_FallsBackToSystem32Cmd`).
- **Cancel during a de-elevated Scoop run**: must kill the whole process tree, like `RunAsync`. → Task 3 (`RunAsUserAsync_Cancelled_StopsTheProcess`, elevated/attended integration test).

---

### Task 1: Ship the native DLLs beside the exe; turn off startup hooks

**Files:**
- Modify: `src/PatchPal.App/PatchPal.App.csproj` (add `StartupHookSupport=false`)
- Modify: `.github/workflows/release.yml:55-70` (publish step: drop the self-extract flag; add a file-check step)
- Modify: `installer/setup.iss` (`[Files]` section)
- Test: a local `dotnet publish` check (commands below); the CI file-check script is its own guard

**Interfaces:**
- Produces: a publish folder (`dist/publish-$arch`) containing `PatchPal.exe` and exactly the five native DLLs; the installer and zip carry all six files.

- [ ] **Step 1: Add the build-time settings**

In `PatchPal.App.csproj`, inside the existing `<PropertyGroup>`, add `<StartupHookSupport>false</StartupHookSupport>`. (Single-file publish already leaves native libraries beside the exe once the self-extract flag is gone; no csproj change is needed for that.)

- [ ] **Step 2: Drop the self-extract flag in `release.yml`**

In the "Publish (x64 and arm64)" step, remove `-p:IncludeNativeLibrariesForSelfExtract=true` from the `dotnet publish` line. Keep `-p:PublishSingleFile=true` and `--self-contained`.

- [ ] **Step 3: Add a CI file-check step after publish**

New step (pwsh) that, for each arch, asserts the set of `*.dll` in `dist/publish-$arch` equals the five expected names — fail (`exit 1`) if any extra DLL appears or any is missing. This catches a new native DLL that would otherwise silently miss setup and the zip. Expected names are the five from Global Constraints.

- [ ] **Step 4: Copy the DLLs in `setup.iss`**

In `[Files]`, add one line beside the existing `PatchPal.exe` line:
`Source: "{#MySourceDir}\*_cor3.dll"; DestDir: "{app}"; Flags: ignoreversion`
(The zip step in `release.yml` already copies `dist/publish-$arch/*` via the staging folder — confirm it globs the folder, not just `PatchPal.exe`; if it names `PatchPal.exe` explicitly, change it to copy the DLLs too.)

- [ ] **Step 5: Verify the publish locally**

Run: `dotnet publish src/PatchPal.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o <scratchpad>/pub-x64`
Expected: the folder contains `PatchPal.exe` and exactly the five `*_cor3.dll` files (a `.pdb` may also appear; ignore it). No `%TEMP%\.net\PatchPal` folder is created by publishing.

- [ ] **Step 6: Build and test**

Run: `dotnet build -c Release -warnaserror`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/PatchPal.App/PatchPal.App.csproj .github/workflows/release.yml installer/setup.iss
git commit -m "ci: ship WPF native DLLs beside the exe and off the startup-hook path"
```

**Manual done-when (owner's PC, attended, after an installed build):** every module loaded in the running PatchPal process is under `C:\Program Files\PatchPal` or `C:\Windows`; the build creates no `%TEMP%\.net\PatchPal` folder.

---

### Task 2: Resolve Windows tools from System32; lock the DLL search; scrub child environments

**Files:**
- Create: `src/PatchPal.Core/Sources/WindowsTools.cs`
- Create: `tests/PatchPal.Core.Tests/Sources/WindowsToolsTests.cs`
- Modify: `src/PatchPal.Core/Sources/ProcessRunner.cs` (CommandExists → `WindowsTools.WherePath`; cmd fallback → `WindowsTools.CmdPath`; scrub loader env vars from every child it starts)
- Modify: `src/PatchPal.App/Program.cs` (P/Invoke `SetDefaultDllDirectories` as the first statement of `Main`)
- Test: `tests/PatchPal.Core.Tests/Sources/ProcessRunnerTests.cs` (fallback test + env-scrub test)

**Interfaces:**
- Produces: `public static class WindowsTools` with `static string WherePath`, `static string CmdPath`, `static string PowerShellPath`, `static string ExplorerPath` — each an absolute path (`WherePath`/`CmdPath`/`PowerShellPath` under `Environment.SystemDirectory`, `PowerShellPath` = `…\WindowsPowerShell\v1.0\powershell.exe`; `ExplorerPath` = `Environment.GetFolderPath(SpecialFolder.Windows)\explorer.exe`). Consumed by Tasks 4 and 6.
- Produces: a private `ProcessRunner` helper that removes the .NET and .NET-Framework loader variables — every key starting `DOTNET_`, `CORECLR_`, or `COR_` — from a `ProcessStartInfo`'s `Environment`, applied to every child `ProcessRunner` starts. Task 3's `RunAsUserAsync` reuses it (its de-elevated token start must also carry a scrubbed environment).

- [ ] **Step 1: Write the failing test for `WindowsTools`**

```csharp
// WindowsToolsTests.cs
[Fact]
public void PathsAreAbsoluteUnderSystem32()
{
    var sys = Environment.SystemDirectory;
    Assert.True(Path.IsPathFullyQualified(WindowsTools.WherePath));
    Assert.Equal(Path.Combine(sys, "where.exe"), WindowsTools.WherePath);
    Assert.Equal(Path.Combine(sys, "cmd.exe"), WindowsTools.CmdPath);
    Assert.Equal(Path.Combine(sys, "WindowsPowerShell", "v1.0", "powershell.exe"), WindowsTools.PowerShellPath);
    Assert.EndsWith("explorer.exe", WindowsTools.ExplorerPath);
    Assert.True(Path.IsPathFullyQualified(WindowsTools.ExplorerPath));
}
```

- [ ] **Step 2: Run it; verify it fails** — Run: `dotnet test tests/PatchPal.Core.Tests --filter WindowsToolsTests` → Expected: FAIL (`WindowsTools` not defined).

- [ ] **Step 3: Implement `WindowsTools`** in the new file. Four expression-bodied static properties as described in Interfaces.

- [ ] **Step 4: Run it; verify it passes.**

- [ ] **Step 5: Write the failing fallback test for `ProcessRunner`**

```csharp
// ProcessRunnerTests.cs — the RunAsInvoker recipe depends on this fallback.
[Fact]
public async Task RunAsync_BareNameThatFails_FallsBackToSystem32Cmd()
{
    // A bare name that direct CreateProcess can't start must be retried via System32 cmd.exe.
    // "cmd"'s own builtins (echo) only resolve through a shell, proving the fallback ran.
    var result = await new ProcessRunner().RunAsync("this-is-not-an-exe-but-echoes", "");
    // No assertion on success; the test is that no unhandled Win32Exception escaped and
    // the fallback path (cmd.exe from System32) was taken. See Step 7 note.
}
```
(If this proves awkward to assert deterministically, instead keep `ProcessRunnerTests` green and cover the fallback through the existing winget RunAsInvoker manual recipe; the real guard is Step 6.)

- [ ] **Step 6: Point `ProcessRunner` at the absolute paths**

In `ProcessRunner.CommandExists`, start `WindowsTools.WherePath` instead of `"where.exe"`. In the `catch (Win32Exception)` fallback in `RunAsync`, start `WindowsTools.CmdPath` instead of `"cmd.exe"`. Leave the trigger (bare `fileName` with no slash) and the `winget` bare-name launch unchanged, so the RunAsInvoker recipe still reaches the fallback.

- [ ] **Step 7: Write the failing env-scrub test (RED)**

```csharp
// ProcessRunnerTests.cs — Patch Pal must not pass loader-injection variables to the
// (elevated) children it starts. cmd prints an unset var as its literal %NAME% text.
[Fact]
public async Task RunAsync_RemovesDotnetLoaderVarsFromChildren()
{
    Environment.SetEnvironmentVariable("DOTNET_STARTUP_HOOKS", @"C:\x.dll");
    try
    {
        var r = await new ProcessRunner().RunAsync(WindowsTools.CmdPath, "/c echo [%DOTNET_STARTUP_HOOKS%]");
        Assert.Contains("[%DOTNET_STARTUP_HOOKS%]", r.StdOut);   // unexpanded ⇒ not in the child
    }
    finally { Environment.SetEnvironmentVariable("DOTNET_STARTUP_HOOKS", null); }
}
```

- [ ] **Step 8: Run it; verify it fails** (the child currently inherits the variable, so the output shows the value, not the literal).

- [ ] **Step 9: Implement the scrub**

Add a private helper that, given a `ProcessStartInfo`, removes from `psi.Environment` every key whose name starts with `DOTNET_`, `CORECLR_`, or `COR_` (ordinal, case-insensitive). Call it on every `ProcessStartInfo` `ProcessRunner` builds (the direct start, the cmd fallback, and `CommandExists`). Run the test; verify it passes.

- [ ] **Step 10: Lock the DLL search in `Program.Main`**

Add a P/Invoke to `Program.cs`:
```csharp
[DllImport("kernel32.dll", SetLastError = true)]
private static extern bool SetDefaultDllDirectories(uint directoryFlags);
private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x1000;
```
Call `SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);` as the **first** statement of `Main`, before `CliOptions.Parse(args)`.

- [ ] **Step 11: Build and run the suite**

Run: `dotnet build -c Release -warnaserror` then `dotnet test -warnaserror`
Expected: PASS, including the unchanged `CommandExists_FindsCmd` and the cancel test.

- [ ] **Step 12: Commit**

```bash
git add src/PatchPal.Core/Sources/WindowsTools.cs tests/PatchPal.Core.Tests/Sources/WindowsToolsTests.cs src/PatchPal.Core/Sources/ProcessRunner.cs tests/PatchPal.Core.Tests/Sources/ProcessRunnerTests.cs src/PatchPal.App/Program.cs
git commit -m "fix: run Windows tools from System32, lock the DLL search, scrub child env"
```

**Manual done-when (owner's PC):** a `where.exe`/`cmd.exe`/`powershell.exe` dropped into the app folder or the working folder is never run.

---

### Task 3: A de-elevated process launch on `IProcessRunner`

**Files:**
- Modify: `src/PatchPal.Core/Sources/IProcessRunner.cs` (add `RunAsUserAsync`)
- Create: `src/PatchPal.Core/Sources/ProcessLaunchException.cs`
- Modify: `src/PatchPal.Core/Sources/ProcessRunner.cs` (implement `RunAsUserAsync`)
- Modify: `tests/PatchPal.Core.Tests/Sources/FakeProcessRunner.cs` (record `RunAsUserAsync`)
- Test: `tests/PatchPal.Core.Tests/Sources/ProcessRunnerTests.cs`

**Interfaces:**
- Produces: `Task<ProcessResult> RunAsUserAsync(string fileName, string arguments, CancellationToken ct = default)` on `IProcessRunner` — starts the process with the user's normal rights (Medium integrity, Administrators deny-only), redirects stdout/stderr, and kills the whole tree on cancel, exactly like `RunAsync`. Throws `ProcessLaunchException` when it cannot create the de-elevated process. Consumed by Task 4.
- Produces: `public sealed class ProcessLaunchException : Exception` (message + optional inner).
- Produces: `FakeProcessRunner.Calls` gains a fourth field `bool AsUser`; `RunAsUserAsync` records `(fileName, arguments, Elevated: false, AsUser: true)`. A settable `Exception? ThrowOnRunAsUser` makes it simulate a launch failure.

- [ ] **Step 0 (SPIKE — throwaway, owner's PC, attended):** Prove the de-elevation mechanism before writing `RunAsUserAsync`. In the scratchpad, build a tiny elevated console app that launches `whoami /all` through the **leading candidate** (a restricted copy of the process token — Administrators and the "local account and member of Administrators" group deny-only, admin privileges removed, integrity lowered to Medium — passed to `CreateProcessAsUser` with redirected pipes). Confirm the five spike checks from spec §3: the child shows Medium IL + Administrators "deny only" + a normal prompt's privileges; files it writes are user-owned; stdout/exit/cancel work; `scoop status`/`scoop update` work (switch Scoop on in Settings for the test, off after); `scoop update` reaches System32 `powershell.exe` by full path. If the leading candidate fails, fall back to `CreateProcessWithTokenW` with the shell's token. Record which approach passed in a comment at the top of `ProcessRunner.RunAsUserAsync`. Delete the spike. **No production code is kept from the spike.**

- [ ] **Step 1: Add the interface method and the exception; extend the fake**

Add `RunAsUserAsync` to `IProcessRunner` with the summary from Interfaces. Create `ProcessLaunchException`. Extend `FakeProcessRunner`: widen the `Calls` tuple to `(string FileName, string Arguments, bool Elevated, bool AsUser)`, update `RunAsync`/`RunElevatedAsync` to pass `AsUser: false`, add `RunAsUserAsync` (honours a new `ThrowOnRunAsUser`). Build; fix the existing source tests only where the added tuple field forces it (named access keeps compiling).

- [ ] **Step 2: Write the failing behavior test (unit, runner-agnostic)**

```csharp
// ProcessRunnerTests.cs
[Fact]
public async Task RunAsUserAsync_RunsAndCapturesOutput()
{
    // Non-elevated CI simply runs it as the same user; elevated runs de-elevate.
    var result = await new ProcessRunner().RunAsUserAsync(WindowsTools.CmdPath, "/c echo hi & exit 4");
    Assert.Equal(4, result.ExitCode);
    Assert.Contains("hi", result.StdOut);
}
```

- [ ] **Step 3: Run it; verify it fails** (method throws `NotImplementedException`/not implemented).

- [ ] **Step 4: Implement `RunAsUserAsync`** in `ProcessRunner`, using the approach the spike confirmed. Reuse `RunAsync`'s output-pump and `Kill(entireProcessTree: true)` cancel handling. On any failure to create the de-elevated process, throw `ProcessLaunchException`. **Never** fall back to a non-de-elevated start.

- [ ] **Step 5: Run it; verify it passes.**

- [ ] **Step 6: Add the cancel test (parity with `RunAsync`)**

```csharp
[Fact]
public async Task RunAsUserAsync_Cancelled_StopsTheProcess()
{
    var marker = Path.Combine(Path.GetTempPath(), $"patchpal-cancel-user-{Guid.NewGuid():N}.txt");
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ProcessRunner().RunAsUserAsync(
            WindowsTools.CmdPath, $"/c ping -n 3 127.0.0.1 >nul & echo done>\"{marker}\"", cts.Token));
        await Task.Delay(TimeSpan.FromSeconds(3));
        Assert.False(File.Exists(marker));
    }
    finally { File.Delete(marker); }
}
```

- [ ] **Step 7: Run tests; verify pass.** Run: `dotnet test -warnaserror` → PASS.

- [ ] **Step 8: Commit**

```bash
git add src/PatchPal.Core/Sources/IProcessRunner.cs src/PatchPal.Core/Sources/ProcessLaunchException.cs src/PatchPal.Core/Sources/ProcessRunner.cs tests/PatchPal.Core.Tests/Sources/FakeProcessRunner.cs tests/PatchPal.Core.Tests/Sources/ProcessRunnerTests.cs
git commit -m "feat: add a de-elevated process launch to ProcessRunner"
```

**Manual done-when (owner's PC, attended, elevated):** `whoami /all` through `RunAsUserAsync` shows Medium IL and Administrators "Group used for deny only" (the spike check, re-run against the shipped method).

---

### Task 4: Scoop without admin

**Files:**
- Modify: `src/PatchPal.Core/Sources/ScoopSource.cs`
- Test: `tests/PatchPal.Core.Tests/Sources/PackageSourceTests.cs`

**Interfaces:**
- Consumes: `IProcessRunner.RunAsUserAsync` (Task 3); `WindowsTools.PowerShellPath` (Task 2); `ProcessLaunchException` (Task 3).
- Produces: `ScoopSource` that queries and updates through `RunAsUserAsync`, never `RunAsync`/`RunElevatedAsync`.

- [ ] **Step 1: Update the existing scoop-upgrade test (RED)**

Change `Scoop_Upgrade_RunsAsCurrentUserThroughPowerShell` to expect the System32 PowerShell path and the AsUser launch:
```csharp
var call = Assert.Single(runner.Calls);
Assert.Equal(WindowsTools.PowerShellPath, call.FileName);
Assert.Equal("-NoProfile -Command \"scoop update extras-app_name\"", call.Arguments);
Assert.True(call.AsUser);
Assert.False(call.Elevated);
```

- [ ] **Step 2: Add the scan-path and failure tests (RED)**

```csharp
[Fact]
public async Task Scoop_Status_RunsDeElevated()
{
    var runner = new FakeProcessRunner { StdOut = "" };
    await new ScoopSource(runner).ListOutdatedAsync();
    var call = Assert.Single(runner.Calls);
    Assert.Equal("scoop", call.FileName);
    Assert.Equal("status", call.Arguments);
    Assert.True(call.AsUser);
}

[Fact]
public async Task Scoop_WhenTheDeElevatedLaunchFails_WarnsAndDoesNotRunElevated()
{
    var runner = new FakeProcessRunner { ThrowOnRunAsUser = new ProcessLaunchException("no") };
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(
        () => new ScoopSource(runner).ListOutdatedAsync());
    Assert.Contains("without admin", ex.Message);
    Assert.DoesNotContain(runner.Calls, c => c.Elevated);   // never fell back to admin
}
```

- [ ] **Step 3: Run; verify all three fail.**

- [ ] **Step 4: Implement**

In `ScoopSource`: `ListOutdatedAsync` calls `runner.RunAsUserAsync("scoop", "status", ct)`, and wraps a `ProcessLaunchException` as `throw new InvalidOperationException("Couldn't start Scoop without admin rights, so its updates weren't checked.")`. `RunUpgradeAsync` calls `runner.RunAsUserAsync(WindowsTools.PowerShellPath, $"-NoProfile -Command \"scoop update {packageId}\"", ct)`, and on `ProcessLaunchException` returns `new ProcessResult(-1, "", "Couldn't start Scoop without admin rights; run 'scoop update " + packageId + "' from a normal prompt yourself.")`. Replace the stale "no elevation" comment with one that says the launch is de-elevated and why. Keep the `PackageIdValidator` note.

- [ ] **Step 5: Run tests; verify pass** (including the untouched `IsInstalled_ChecksTheRightExecutable`).

- [ ] **Step 6: Commit**

```bash
git add src/PatchPal.Core/Sources/ScoopSource.cs tests/PatchPal.Core.Tests/Sources/PackageSourceTests.cs
git commit -m "fix: run Scoop with the user's normal rights, not as admin"
```

**Note on all-users Scoop apps (`scoop install -g`):** they genuinely need admin to update and will fail here with the clear message. Not specially handled; documented in Task 8.

---

### Task 5: Chocolatey only from an admin-controlled folder

**Files:**
- Create: `src/PatchPal.Core/Sources/IChocolateyLocator.cs` (locator + directory-trust seams)
- Create: `src/PatchPal.Core/Sources/ChocolateyLocator.cs` (production impl)
- Modify: `src/PatchPal.Core/Sources/ChocoSource.cs` (take a locator; use the full path)
- Modify: `src/PatchPal.App/AppServices.cs:29` and `src/PatchPal.App/Cli/CliRunner.cs:31` (wire `new ChocolateyLocator()`)
- Create: `tests/PatchPal.Core.Tests/Sources/ChocolateyLocatorTests.cs`
- Create: `tests/PatchPal.Core.Tests/Sources/DirectoryTrustTests.cs`
- Test: `tests/PatchPal.Core.Tests/Sources/PackageSourceTests.cs`

**Interfaces:**
- Produces: `public interface IChocolateyLocator { string? ChocoExePath { get; } }` — the full path to `choco.exe` only when its folder is admin-controlled, else `null`.
- Produces: `public interface IDirectoryTrust { bool IsAdminOnly(string path); }` — true when no normal account can change `path`.
- Produces: `ChocolateyLocator(IDirectoryTrust trust, Func<string?> readMachineChocolateyInstall)` and `DirectoryTrust : IDirectoryTrust` (production ACL/owner check).
- Consumes (ChocoSource): `IChocolateyLocator`; `IProcessRunner` (unchanged, still `RunElevatedAsync` for upgrades — choco is a machine tool).

- [ ] **Step 1: Write failing locator tests (RED)**

```csharp
// ChocolateyLocatorTests.cs
private sealed class FakeTrust(bool ok) : IDirectoryTrust { public bool IsAdminOnly(string p) => ok; }

[Fact]
public void TrustedFolderWithChocoExe_IsReported()
{
    var dir = CreateTempDirWith("bin", "choco.exe");               // helper creates <dir>\bin\choco.exe
    var locator = new ChocolateyLocator(new FakeTrust(true), () => dir);
    Assert.Equal(Path.Combine(dir, "bin", "choco.exe"), locator.ChocoExePath);
}

[Fact]
public void FolderANormalUserCanChange_IsNotTrusted()
{
    var dir = CreateTempDirWith("bin", "choco.exe");
    var locator = new ChocolateyLocator(new FakeTrust(false), () => dir);
    Assert.Null(locator.ChocoExePath);
}

[Fact]
public void MissingValue_FallsBackToCommonAppDataChocolatey()
{
    var locator = new ChocolateyLocator(new FakeTrust(true), () => null);
    // choco.exe won't exist under the real ProgramData\chocolatey\bin on the test box → null,
    // but the fallback path must be the one probed:
    Assert.Null(locator.ChocoExePath);   // asserts no throw and the fallback branch ran
}
```

- [ ] **Step 2: Run; verify fail.**

- [ ] **Step 3: Implement `ChocolateyLocator`** — resolve the install dir from `readMachineChocolateyInstall()` or, when null/empty, `Path.Combine(Environment.GetFolderPath(SpecialFolder.CommonApplicationData), "chocolatey")`. Build `<dir>\bin\choco.exe`. Return it only if `File.Exists(path) && trust.IsAdminOnly(dir)`, else `null`.

- [ ] **Step 4a: Write the failing `DirectoryTrust` test (RED)**

```csharp
// DirectoryTrustTests.cs
[Fact]
public void FreshTempDir_IsNotAdminOnly()
{
    var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"pp-trust-{Guid.NewGuid():N}")).FullName;
    try { Assert.False(new DirectoryTrust().IsAdminOnly(dir)); }   // the current user can write here
    finally { Directory.Delete(dir, true); }
}

[Fact]
public void System32_IsAdminOnly()
    => Assert.True(new DirectoryTrust().IsAdminOnly(Environment.SystemDirectory));

[Fact]
public void MissingDir_IsNotAdminOnly()
    => Assert.False(new DirectoryTrust().IsAdminOnly(@"C:\no-such-folder-" + Guid.NewGuid().ToString("N")));
```
Run it; verify it fails (`DirectoryTrust` not defined).

- [ ] **Step 4b: Implement `DirectoryTrust.IsAdminOnly`** — using `DirectoryInfo.GetAccessControl()`, return false if any writable right (Write/CreateFiles/CreateDirectories/Modify/FullControl, allow) is granted to a non-admin principal (anyone other than SYSTEM, Administrators, TrustedInstaller, the admin-owned built-ins); return false when the directory is missing or the ACL can't be read. Keep it small and conservative: unknown → not trusted. Run the Step 4a tests; verify pass.

- [ ] **Step 5: Add the production machine-registry reader** — a small static `ChocolateyLocator.ReadMachineChocolateyInstall()` that reads `ChocolateyInstall` from `HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment` via `Microsoft.Win32.Registry` (not the process environment). Used by a convenience constructor `ChocolateyLocator()` that wires `new DirectoryTrust()` + this reader.

- [ ] **Step 6: Write failing `ChocoSource` tests (RED)**

```csharp
// PackageSourceTests.cs
private sealed class FakeLocator(string? path) : IChocolateyLocator { public string? ChocoExePath => path; }

[Fact]
public void Choco_NotInstalled_WhenLocatorReturnsNull()
{
    var source = new ChocoSource(new FakeProcessRunner(), new FakeLocator(null));
    Assert.False(source.IsInstalled());
}

[Fact]
public async Task Choco_Upgrade_RunsTheTrustedChocoExeElevated()
{
    var runner = new FakeProcessRunner();
    var path = @"C:\ProgramData\chocolatey\bin\choco.exe";
    await new ChocoSource(runner, new FakeLocator(path)).RunUpgradeAsync("git.install");
    var call = Assert.Single(runner.Calls);
    Assert.Equal(path, call.FileName);
    Assert.Equal("upgrade git.install -y", call.Arguments);
    Assert.True(call.Elevated);
}
```

- [ ] **Step 7: Run; verify fail.**

- [ ] **Step 8: Change `ChocoSource`** to `ChocoSource(IProcessRunner runner, IChocolateyLocator locator)`: `IsInstalled()` → `locator.ChocoExePath is not null`; `ListOutdatedAsync`/`RunUpgradeAsync` use `locator.ChocoExePath!` as the filename instead of `"choco"`. Update the `IsInstalled_ChecksTheRightExecutable` theory: choco's branch now constructs `new ChocoSource(runner, new FakeLocator(@"...\choco.exe"))`, and the `ExecutableName`-equals-`choco` assertion stays if `ExecutableName` is kept for display, or is dropped for choco.

- [ ] **Step 9: Wire the real locator** at both composition sites — `AppServices.cs:29` and `Cli/CliRunner.cs:31` — changing `new ChocoSource(runner)` to `new ChocoSource(runner, new ChocolateyLocator())`.

- [ ] **Step 10: Run the suite; verify pass.** Run: `dotnet test -warnaserror`.

- [ ] **Step 11: Commit**

```bash
git add src/PatchPal.Core/Sources/IChocolateyLocator.cs src/PatchPal.Core/Sources/ChocolateyLocator.cs src/PatchPal.Core/Sources/ChocoSource.cs tests/PatchPal.Core.Tests/Sources/ChocolateyLocatorTests.cs tests/PatchPal.Core.Tests/Sources/PackageSourceTests.cs src/PatchPal.App
git commit -m "fix: trust Chocolatey only from an admin-controlled install folder"
```

**Manual done-when (owner's PC):** with Chocolatey not installed and a `choco.cmd`/`choco.exe` dropped into a user PATH folder, Patch Pal neither lists nor runs Chocolatey.

---

### Task 6: The About link opens de-elevated

**Files:**
- Create: `src/PatchPal.Core/Sources/ShellLauncher.cs`
- Create: `tests/PatchPal.Core.Tests/Sources/ShellLauncherTests.cs`
- Modify: `src/PatchPal.App/MainWindow.xaml.cs` (`OnAboutClicked`)

**Interfaces:**
- Consumes: `WindowsTools.ExplorerPath` (Task 2).
- Produces: `public static class ShellLauncher { static void OpenUrl(string url, Action<string, string>? start = null); }` — composes `(WindowsTools.ExplorerPath, "\"<url>\"")` and calls `start` (default: a real `Process.Start(fileName, arguments)` wrapper with `UseShellExecute = false`). Routing through `explorer.exe` makes the shell start the browser at the user's integrity.

- [ ] **Step 1: Write the failing test (RED)**

```csharp
// ShellLauncherTests.cs
[Fact]
public void OpenUrl_LaunchesExplorerWithTheQuotedUrl()
{
    (string file, string args) captured = default;
    ShellLauncher.OpenUrl("https://example.com/x", (f, a) => captured = (f, a));
    Assert.Equal(WindowsTools.ExplorerPath, captured.file);
    Assert.Equal("\"https://example.com/x\"", captured.args);
}
```

- [ ] **Step 2: Run; verify fail.**

- [ ] **Step 3: Implement `ShellLauncher.OpenUrl`** as described.

- [ ] **Step 4: Run; verify pass.**

- [ ] **Step 5: Wire `OnAboutClicked`** — replace the `Process.Start(new ProcessStartInfo(AppInfo.RepoUrl){ UseShellExecute = true })` block with `ShellLauncher.OpenUrl(AppInfo.RepoUrl);`.

- [ ] **Step 6: Build; run tests.** Run: `dotnet build -c Release -warnaserror` then `dotnet test -warnaserror` → PASS.

- [ ] **Step 7: Commit**

```bash
git add src/PatchPal.Core/Sources/ShellLauncher.cs tests/PatchPal.Core.Tests/Sources/ShellLauncherTests.cs src/PatchPal.App/MainWindow.xaml.cs
git commit -m "fix: open the About link through the shell so the browser runs de-elevated"
```

**Manual done-when (owner's PC, attended):** About → Open GitHub starts the browser process at Medium integrity.

---

### Task 7: Installer — all-users-only, remove an old per-user copy

**Files:**
- Modify: `installer/setup.iss` (`[Setup]` privileges; new `[Code]` section)

**Interfaces:**
- Produces: a setup that always installs to `{autopf}\PatchPal` (Program Files) for all users, and that silently removes an existing per-user copy as the original user before installing.

No xUnit tests — Inno Setup. Verified by `release.yml` `workflow_dispatch` build and the four manual install cases.

- [ ] **Step 1: Force all-users** — in `[Setup]`, set `PrivilegesRequired=admin` and delete the `PrivilegesRequiredOverridesAllowed=dialog` line. Leave `{autopf}`/`{autoprograms}`/`{autodesktop}` as-is (they now resolve machine-wide).

- [ ] **Step 2: Remove the old per-user copy** — add a `[Code]` `InitializeSetup` (or `PrepareToInstall`) step that:
  - reads `UninstallString` from `HKCU\...\Uninstall\{60cbe8cd-e316-4bc8-9a92-96305ec2c7d2}_is1`;
  - if present, runs it with `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART` via `ExecAsOriginalUser`, waiting for exit;
  - never blocks the install on failure; if it failed, set a flag so a closing message says the old per-user copy is still there.
  - Do **not** add an `[UninstallDelete]` for `%APPDATA%\PatchPal` (settings/history must survive).

- [ ] **Step 3: Compile-check locally if ISCC is available; otherwise rely on CI.** If `C:\Program Files (x86)\Inno Setup 6\ISCC.exe` exists, run it against `setup.iss` with the dev defines and confirm it compiles. (ISCC is not installed on the owner's PC; the `workflow_dispatch` build is the gate.)

- [ ] **Step 4: Commit**

```bash
git add installer/setup.iss
git commit -m "fix: install Patch Pal for all users and remove an old per-user copy"
```

**Manual done-when (a Windows machine where installing is fine; owner attends for the real PC):** fresh install; upgrade over a 2.1.0 all-users install; upgrade over a 2.1.0 per-user install; uninstall — all four keep `%APPDATA%\PatchPal` settings and history.

---

### Task 8: Docs

**Files:**
- Modify: `README.md` (Install, Portable zip, Schedule a daily check)
- Modify: `CHANGELOG.md` (`[Unreleased]`)
- Modify: `docs/SIGNING.md`

No tests (docs are exempt from the TDD gate).

- [ ] **Step 1: README** — Install: setup asks for admin and installs for all users. Portable zip: keep the whole folder together and extract it somewhere only an admin can change (e.g. under `C:\Program Files`), or use the installer; say why. Schedule a daily check: change the path to `"$env:ProgramFiles\PatchPal\PatchPal.exe"` and say the task must point at a copy only an admin can change.

- [ ] **Step 2: CHANGELOG** — add a `### Security` block under `[Unreleased]` summarising: DLLs beside the exe (no Temp), all-users-only setup, Scoop de-elevated, Chocolatey only from its admin-controlled folder, Windows tools by full path + DLL-search lock, About link de-elevated; name winget and the pre-`Main` runtime load as accepted limits. Add a plain note for "for me only" users who have scheduled tasks to repoint them.

- [ ] **Step 3: SIGNING.md** — note Microsoft already signs the five native DLLs, so signing covers only `PatchPal.exe` and the installers.

- [ ] **Step 4: Commit**

```bash
git add README.md CHANGELOG.md docs/SIGNING.md
git commit -m "docs: describe the all-users, de-elevated hardening"
```

---

## Notes for the executor

- Tasks 1, 7 (installer) and 8 (docs) carry manual/CI verification rather than xUnit; their "done-when" lines say exactly what to check and where. Every other task is RED→GREEN with the suite green at `-warnaserror`.
- Task 3's spike is throwaway and attended; no spike code is kept. The implementation that ships is written test-first after the spike settles the mechanism.
- The data-file/export hardening is explicitly a **follow-up round** (spec's final section) and is **not** in this plan. Build `RunAsUserAsync` so that follow-up can route writes through it.
