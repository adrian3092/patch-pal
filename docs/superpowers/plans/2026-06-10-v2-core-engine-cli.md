# WinUpdateChecker v2; Plan 1 of 3: Core Engine + CLI

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `WinUpdateChecker.Core` (the complete scan/match/upgrade engine, ported 1:1 from `UpdateChecker.ps1`) plus a working headless CLI, fully unit-tested.

**Architecture:** Three-project .NET 8 solution per the spec (`docs/superpowers/specs/2026-06-10-wpf-rewrite-design.md`). All logic lives in `WinUpdateChecker.Core` with zero UI references; package-manager CLIs are wrapped behind `IProcessRunner` so every parser and source is testable with fixtures. The WPF App project is scaffolded with a placeholder window; the real GUI is Plan 2. After this plan, `WinUpdateChecker.exe --no-gui` is a working v2 replacement for `UpdateChecker.ps1 -NoGui`.

**Tech Stack:** C# / .NET 8 (`net8.0-windows`), WPF (placeholder only), xUnit.

**Porting source of truth:** `UpdateChecker.ps1` (v1.0.3) and `tests/Merge.Tests.ps1`. Where this plan shows C# logic, it is a direct port; do not "improve" matching/filter behavior; the xUnit tests below encode the exact v1 semantics.

---

### Task 1: Scaffold solution

**Files:**
- Create: `WinUpdateChecker.sln`, `src/WinUpdateChecker.Core/WinUpdateChecker.Core.csproj`, `src/WinUpdateChecker.App/` (WPF template), `tests/WinUpdateChecker.Core.Tests/WinUpdateChecker.Core.Tests.csproj`
- Modify: `.gitignore`

- [ ] **Step 1: Verify the .NET 8 SDK is installed**

Run: `dotnet --list-sdks`
Expected: at least one `8.x` line. If missing: `winget install Microsoft.DotNet.SDK.8` and re-open the shell.

- [ ] **Step 2: Create solution and projects**

Run from the repo root:

```powershell
dotnet new sln -n WinUpdateChecker
dotnet new classlib -n WinUpdateChecker.Core -o src/WinUpdateChecker.Core
dotnet new wpf -n WinUpdateChecker.App -o src/WinUpdateChecker.App
dotnet new xunit -n WinUpdateChecker.Core.Tests -o tests/WinUpdateChecker.Core.Tests
dotnet sln add src/WinUpdateChecker.Core src/WinUpdateChecker.App tests/WinUpdateChecker.Core.Tests
dotnet add src/WinUpdateChecker.App reference src/WinUpdateChecker.Core
dotnet add tests/WinUpdateChecker.Core.Tests reference src/WinUpdateChecker.Core
```

- [ ] **Step 3: Set TargetFramework to `net8.0-windows` in all three csproj files**

The Core library reads the registry, which requires the Windows TFM. Replace the `<TargetFramework>` line in `src/WinUpdateChecker.Core/WinUpdateChecker.Core.csproj` and `tests/WinUpdateChecker.Core.Tests/WinUpdateChecker.Core.Tests.csproj` with:

```xml
<TargetFramework>net8.0-windows</TargetFramework>
```

(`src/WinUpdateChecker.App` already targets `net8.0-windows` from the WPF template.) Delete the template-generated `src/WinUpdateChecker.Core/Class1.cs`.

- [ ] **Step 4: Add build artifacts to .gitignore**

Append to `.gitignore`:

```
# .NET build artifacts
bin/
obj/
*.binlog
```

- [ ] **Step 5: Verify build and test run**

Run: `dotnet build` then `dotnet test`
Expected: build succeeds; 1 placeholder test passes.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "chore: scaffold v2 .NET solution (Core, App, Core.Tests)"
```

---

### Task 2: Version logic

Ports `Get-VersionValue` and `Test-IsNewerVersion` (UpdateChecker.ps1:296-317).

**Files:**
- Create: `src/WinUpdateChecker.Core/Matching/VersionLogic.cs`
- Test: `tests/WinUpdateChecker.Core.Tests/Matching/VersionLogicTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using WinUpdateChecker.Core.Matching;

namespace WinUpdateChecker.Core.Tests.Matching;

public class VersionLogicTests
{
    // Ports tests/Merge.Tests.ps1 "Version comparison"
    [Theory]
    [InlineData("14.44.35211.0", "14.51.36231.0", true)]   // older -> update
    [InlineData("14.51.36231.0", "14.51.36231.0", false)]  // equal -> no update
    [InlineData("14.52.0.0", "14.51.36231.0", false)]      // installed newer -> no update
    public void IsNewerVersion_ComparesParsableVersions(string current, string available, bool expected)
        => Assert.Equal(expected, VersionLogic.IsNewerVersion(current, available));

    [Theory]
    [InlineData("", "1.2.3")]          // unparseable current -> assume update
    [InlineData("abc", "1.2.3")]
    [InlineData("1.2.3", "unknown")]   // unparseable available -> assume update
    public void IsNewerVersion_AssumesUpdateWhenUnparseable(string current, string available)
        => Assert.True(VersionLogic.IsNewerVersion(current, available));

    [Fact]
    public void GetVersionValue_StripsNonVersionCharacters()
        => Assert.Equal(new Version(14, 44, 35211, 0), VersionLogic.GetVersionValue("v14.44.35211.0 (x64)"));

    [Fact]
    public void GetVersionValue_ReturnsZeroWhenUnparseable()
        => Assert.Equal(new Version(0, 0), VersionLogic.GetVersionValue("not a version"));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter VersionLogicTests`
Expected: FAIL; `VersionLogic` does not exist (compile error is the expected failure mode for a new type).

- [ ] **Step 3: Write the implementation**

```csharp
using System.Text.RegularExpressions;

namespace WinUpdateChecker.Core.Matching;

/// <summary>Version parsing and comparison, ported from v1 Get-VersionValue / Test-IsNewerVersion.</summary>
public static partial class VersionLogic
{
    /// <summary>Parse a version-ish string, or 0.0 when it can't be parsed.</summary>
    public static Version GetVersionValue(string? text)
        => Version.TryParse(Clean(text), out var v) ? v : new Version(0, 0);

    /// <summary>
    /// True when <paramref name="available"/> is strictly newer than <paramref name="current"/>.
    /// When either side can't be parsed, assume an update IS available so real updates
    /// are never hidden by an odd version string.
    /// </summary>
    public static bool IsNewerVersion(string? current, string? available)
    {
        if (Version.TryParse(Clean(current), out var c) && Version.TryParse(Clean(available), out var a))
            return a > c;
        return true;
    }

    private static string Clean(string? text)
        => NonVersionChars().Replace(text ?? "", "").Trim('.');

    [GeneratedRegex(@"[^\d.]")]
    private static partial Regex NonVersionChars();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter VersionLogicTests`
Expected: PASS (9 tests).

- [ ] **Step 5: Commit**

```powershell
git add src/WinUpdateChecker.Core/Matching/VersionLogic.cs tests/WinUpdateChecker.Core.Tests/Matching/VersionLogicTests.cs
git commit -m "feat(core): port version comparison logic"
```

---

### Task 3: Name normalizer

Ports `Get-MatchBase` and `Test-BasePrefixMatch` (UpdateChecker.ps1:269-294).

**Files:**
- Create: `src/WinUpdateChecker.Core/Matching/NameNormalizer.cs`
- Test: `tests/WinUpdateChecker.Core.Tests/Matching/NameNormalizerTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using WinUpdateChecker.Core.Matching;

namespace WinUpdateChecker.Core.Tests.Matching;

public class NameNormalizerTests
{
    // Ports tests/Merge.Tests.ps1 "Name normalization"
    [Fact]
    public void GetMatchBase_PreservesEditionToken()
        => Assert.Equal("microsoft visual c++ 2013 redistributable (x64)",
            NameNormalizer.GetMatchBase("Microsoft Visual C++ 2013 Redistributable (x64) - 12.0.40664"));

    [Fact]
    public void GetMatchBase_StripsWingetEllipsisTruncation()
        => Assert.Equal("microsoft visual c++ 2015-2022 redistributable",
            NameNormalizer.GetMatchBase("Microsoft Visual C++ 2015-2022 Redistributable (…"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetMatchBase_EmptyInputYieldsEmpty(string? name)
        => Assert.Equal("", NameNormalizer.GetMatchBase(name));

    [Fact]
    public void IsBasePrefixMatch_RejectsEdgeVsEdgeWebView2()
        => Assert.False(NameNormalizer.IsBasePrefixMatch("microsoft edge", "microsoft edgewebview2 runtime"));

    [Fact]
    public void IsBasePrefixMatch_AcceptsEqualBases()
        => Assert.True(NameNormalizer.IsBasePrefixMatch("git", "git"));

    [Fact]
    public void IsBasePrefixMatch_AcceptsWordBoundaryPrefix()
        => Assert.True(NameNormalizer.IsBasePrefixMatch(
            "microsoft visual c++ 2015-2022 redistributable",
            "microsoft visual c++ 2015-2022 redistributable (x64)"));

    [Fact]
    public void IsBasePrefixMatch_RejectsShortPrefixes()
        => Assert.False(NameNormalizer.IsBasePrefixMatch("git", "git (64-bit)")); // shorter side < 6 chars
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter NameNormalizerTests`
Expected: FAIL; `NameNormalizer` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
using System.Text.RegularExpressions;

namespace WinUpdateChecker.Core.Matching;

/// <summary>Fuzzy name normalization, ported from v1 Get-MatchBase / Test-BasePrefixMatch.</summary>
public static partial class NameNormalizer
{
    /// <summary>
    /// Normalize a program name for fuzzy matching. Strips a trailing dotted version
    /// (e.g. " - 14.44.35211") but PRESERVES edition tokens like "2013" or "2015-2022"
    /// so different product editions never collapse together.
    /// </summary>
    public static string GetMatchBase(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var b = name.ToLowerInvariant();
        b = TrailingDottedVersion().Replace(b, "");
        b = TrailingEllipsis().Replace(b, "");
        b = b.TrimEnd(' ', '-', '(', '[', '/', '+');
        return b.Trim();
    }

    /// <summary>
    /// True when two normalized bases are equal, or one is a word-boundary prefix of the
    /// other. The prefix path handles winget's truncated names without matching unrelated
    /// products (e.g. "Edge" vs "EdgeWebView2").
    /// </summary>
    public static bool IsBasePrefixMatch(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        if (a == b) return true;
        var (longer, shorter) = a.Length >= b.Length ? (a, b) : (b, a);
        if (shorter.Length < 6) return false;
        if (!longer.StartsWith(shorter, StringComparison.Ordinal)) return false;
        var next = longer[shorter.Length];
        return next is ' ' or '(';
    }

    [GeneratedRegex(@"\s*[-–]?\s*v?\d+\.\d[\d.]*\s*$")]
    private static partial Regex TrailingDottedVersion();

    [GeneratedRegex(@"…+$")]
    private static partial Regex TrailingEllipsis();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter NameNormalizerTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/WinUpdateChecker.Core/Matching/NameNormalizer.cs tests/WinUpdateChecker.Core.Tests/Matching/NameNormalizerTests.cs
git commit -m "feat(core): port name normalization and prefix matching"
```

---

### Task 4: Package id validator

Ports `Test-SafePackageId` (UpdateChecker.ps1:431-439): the b95dca8 security fix.

**Files:**
- Create: `src/WinUpdateChecker.Core/Upgrading/PackageIdValidator.cs`
- Test: `tests/WinUpdateChecker.Core.Tests/Upgrading/PackageIdValidatorTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using WinUpdateChecker.Core.Upgrading;

namespace WinUpdateChecker.Core.Tests.Upgrading;

public class PackageIdValidatorTests
{
    // Ports tests/Merge.Tests.ps1 "Package id validation"
    [Theory]
    [InlineData("Microsoft.VCRedist.2015+.x64")]       // winget id with + and .
    [InlineData("Microsoft.DotNet.DesktopRuntime.8")]
    [InlineData("git.install")]                        // choco-style
    [InlineData("extras-app_name")]                    // scoop-style with _ and -
    public void IsSafe_AcceptsLegitimateIds(string id)
        => Assert.True(PackageIdValidator.IsSafe(id));

    [Theory]
    [InlineData("evil; calc.exe")]
    [InlineData("foo & shutdown")]
    [InlineData("a$(rm -rf)")]
    [InlineData("a`nb")]
    [InlineData("pkg with space")]
    [InlineData("")]
    [InlineData(null)]
    public void IsSafe_RejectsInjectionAttempts(string? id)
        => Assert.False(PackageIdValidator.IsSafe(id));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter PackageIdValidatorTests`
Expected: FAIL; `PackageIdValidator` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
using System.Text.RegularExpressions;

namespace WinUpdateChecker.Core.Upgrading;

/// <summary>
/// Package ids are machine-generated identifiers (e.g. Microsoft.VCRedist.2015+.x64,
/// dotnet-sdk, git.install). Restrict to that charset so a crafted name can never inject
/// extra arguments or shell commands into an elevated installer call.
/// Whitelist, not blacklist: anything with a space, quote, ';', '&amp;', '$', '(', '`'
/// or other metacharacter is rejected.
/// </summary>
public static partial class PackageIdValidator
{
    public static bool IsSafe(string? id) => id is not null && SafeId().IsMatch(id);

    [GeneratedRegex(@"^[\w.+-]+$")]
    private static partial Regex SafeId();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter PackageIdValidatorTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/WinUpdateChecker.Core/Upgrading/PackageIdValidator.cs tests/WinUpdateChecker.Core.Tests/Upgrading/PackageIdValidatorTests.cs
git commit -m "feat(core): port package id safety validation"
```

---

### Task 5: Upgrade exit messages

Ports `Get-UpgradeExitMessage` (UpdateChecker.ps1:441-463).

**Files:**
- Create: `src/WinUpdateChecker.Core/Upgrading/UpgradeExitMessages.cs`
- Test: `tests/WinUpdateChecker.Core.Tests/Upgrading/UpgradeExitMessagesTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using WinUpdateChecker.Core.Upgrading;

namespace WinUpdateChecker.Core.Tests.Upgrading;

public class UpgradeExitMessagesTests
{
    [Fact]
    public void ExitZero_ReadsAsSuccess()
        => Assert.Equal("Succeeded.", UpgradeExitMessages.Translate("winget", 0));

    [Fact]
    public void WingetUpdateNotApplicable_IsExplained()
        => Assert.StartsWith("No applicable upgrade", UpgradeExitMessages.Translate("winget", -1978335189));

    [Fact]
    public void NullExitCode_IsExplained()
        => Assert.Equal("No exit code was returned by the installer.", UpgradeExitMessages.Translate("winget", null));

    [Fact]
    public void UnknownWingetCode_IncludesHex()
        => Assert.Equal("winget exited with code -1978335100 (0x8A150084).", UpgradeExitMessages.Translate("winget", -1978335100));

    [Fact]
    public void NonWingetSource_GetsGenericMessage()
        => Assert.Equal("scoop exited with code 2.", UpgradeExitMessages.Translate("scoop", 2));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter UpgradeExitMessagesTests`
Expected: FAIL; `UpgradeExitMessages` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
namespace WinUpdateChecker.Core.Upgrading;

/// <summary>
/// Translates a package-manager exit code into a human-readable result so the UI can
/// tell the user WHY an upgrade did nothing instead of failing silently.
/// </summary>
public static class UpgradeExitMessages
{
    public static string Translate(string source, int? exitCode)
    {
        if (exitCode is null) return "No exit code was returned by the installer.";
        var code = exitCode.Value;
        if (code == 0) return "Succeeded.";
        if (source == "winget")
        {
            return code switch
            {
                -1978335189 => "No applicable upgrade (already current, pinned, or version mismatch).", // 0x8A15002B
                -1978335212 => "No installed package matched for upgrade.",                              // 0x8A150014
                -1978334969 => "No installer applicable to this system.",                                // 0x8A150107
                1602 => "Installer cancelled.",
                1603 => "Fatal installer error (1603): a newer version may already be present.",
                -2147023673 => "Operation cancelled (UAC prompt declined?).",
                _ => $"winget exited with code {code} (0x{(uint)code:X8}).",
            };
        }
        return $"{source} exited with code {code}.";
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter UpgradeExitMessagesTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/WinUpdateChecker.Core/Upgrading/UpgradeExitMessages.cs tests/WinUpdateChecker.Core.Tests/Upgrading/UpgradeExitMessagesTests.cs
git commit -m "feat(core): port upgrade exit-code messages"
```

---

### Task 6: Models + report merger

Ports `Merge-ProgramsAndUpgrades` and `Resolve-DuplicatePackageRows` (UpdateChecker.ps1:319-427): the heart of the tool, including the phantom-row fixes from 6610a06.

**Files:**
- Create: `src/WinUpdateChecker.Core/Models/InstalledProgram.cs`, `src/WinUpdateChecker.Core/Models/UpgradeCandidate.cs`, `src/WinUpdateChecker.Core/Models/ReportRow.cs`, `src/WinUpdateChecker.Core/Matching/ReportMerger.cs`
- Test: `tests/WinUpdateChecker.Core.Tests/Matching/ReportMergerTests.cs`

- [ ] **Step 1: Create the model records (no behavior; no dedicated tests)**

`src/WinUpdateChecker.Core/Models/InstalledProgram.cs`:

```csharp
namespace WinUpdateChecker.Core.Models;

/// <summary>A program found in the Windows uninstall registry keys.</summary>
public sealed record InstalledProgram(string Name, string Version, string? Publisher);
```

`src/WinUpdateChecker.Core/Models/UpgradeCandidate.cs`:

```csharp
namespace WinUpdateChecker.Core.Models;

/// <summary>An outdated package reported by a package manager.</summary>
public sealed record UpgradeCandidate(string Name, string Id, string Current, string Available, string PackageSource);
```

`src/WinUpdateChecker.Core/Models/ReportRow.cs`:

```csharp
namespace WinUpdateChecker.Core.Models;

/// <summary>One merged row of the report: an installed program and/or an available update.</summary>
public sealed record ReportRow(
    string Name,
    string? Publisher,
    string Current,
    string Available,
    string Status,
    string PackageId,
    string PackageSource)
{
    public bool IsUpdate => Status.StartsWith("Update available", StringComparison.Ordinal);
}
```

- [ ] **Step 2: Write the failing merger tests (the full Pester scenario)**

```csharp
using WinUpdateChecker.Core.Matching;
using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Tests.Matching;

public class ReportMergerTests
{
    // Sample data drawn from a real machine (ported from tests/Merge.Tests.ps1):
    // multiple VC++ editions + a stale 14.30 leftover + a separate 2013 (v12) product,
    // plus winget upgrade rows whose names are truncated with an ellipsis.
    private static readonly IReadOnlyList<InstalledProgram> Programs =
    [
        new("Microsoft Visual C++ 2013 Redistributable (x64) - 12.0.40664", "12.0.40664.0", "Microsoft Corporation"),
        new("Microsoft Visual C++ 2015-2022 Redistributable (x64) - 14.30.30708", "14.30.30708.0", "Microsoft Corporation"),
        new("Microsoft Visual C++ 2015-2022 Redistributable (x64) - 14.44.35211", "14.44.35211.0", "Microsoft Corporation"),
        new("Microsoft Visual C++ v14 Redistributable (x86) - 14.50.35719", "14.50.35719.0", "Microsoft Corporation"),
        new("Microsoft Windows Desktop Runtime - 8.0.8 (x64)", "8.0.8.33916", "Microsoft Corporation"),
        new("Google Chrome", "120.0.0.0", "Google LLC"),
    ];

    private static readonly IReadOnlyList<UpgradeCandidate> Upgrades =
    [
        new("Microsoft Visual C++ 2015-2022 Redistributable (…", "Microsoft.VCRedist.2015+.x64", "14.44.35211.0", "14.51.36231.0", "winget"),
        new("Microsoft Visual C++ v14 Redistributable (x86) -…", "Microsoft.VCRedist.2015+.x86", "14.50.35719.0", "14.51.36231.0", "winget"),
        new("Microsoft Windows Desktop Runtime - 8.0.8 (x64)", "Microsoft.DotNet.DesktopRuntime.8", "8.0.8", "8.0.27", "winget"),
    ];

    private static IReadOnlyList<ReportRow> MergedRows()
        => ReportMerger.Merge(Programs, Upgrades, ["winget"]);

    private static IReadOnlyList<ReportRow> UpdateRows()
        => MergedRows().Where(r => r.IsUpdate).ToList();

    [Fact]
    public void Vc2013_IsNotAPhantomUpdate()
    {
        var row2013 = MergedRows().First(r => r.Name.Contains("2013"));
        Assert.False(row2013.IsUpdate);
    }

    [Fact]
    public void StaleVcRedistLeftover_CollapsesToSingleRow()
    {
        var x64 = UpdateRows().Where(r => r.PackageId == "Microsoft.VCRedist.2015+.x64").ToList();
        Assert.Single(x64);
        Assert.Equal("14.44.35211.0", x64[0].Current); // kept the highest installed version
    }

    [Fact]
    public void GenuineUpdates_StillMatch()
    {
        Assert.Single(UpdateRows(), r => r.PackageId == "Microsoft.VCRedist.2015+.x86");
        Assert.Single(UpdateRows(), r => r.PackageId == "Microsoft.DotNet.DesktopRuntime.8");
    }

    [Fact]
    public void ReportsThreeRealUpdates_NotPhantoms()
        => Assert.Equal(3, UpdateRows().Count);

    [Fact]
    public void UnrelatedProgram_IsNotFalselyMatched()
    {
        var chrome = MergedRows().First(r => r.Name == "Google Chrome");
        Assert.False(chrome.IsUpdate);
        Assert.Equal("Up to date / unknown", chrome.Status);
    }

    [Fact]
    public void NoEnabledSources_YieldsNoPackageManagerStatus()
    {
        var rows = ReportMerger.Merge(Programs, [], []);
        Assert.All(rows, r => Assert.Equal("No package manager detected", r.Status));
    }

    [Fact]
    public void UnmatchedUpgrade_AppearsAsSourceOnlyRow()
    {
        var upgrades = new List<UpgradeCandidate> { new("some-cli-tool", "some-cli-tool", "1.0", "2.0", "scoop") };
        var rows = ReportMerger.Merge([new InstalledProgram("Google Chrome", "120.0.0.0", "Google LLC")], upgrades, ["scoop"]);
        var extra = rows.First(r => r.Name == "some-cli-tool");
        Assert.Equal("Update available (scoop only)", extra.Status);
        Assert.Equal("scoop", extra.PackageSource);
    }

    [Fact]
    public void UpdatesSortFirst_ThenByName()
    {
        var rows = MergedRows();
        var firstNonUpdateIndex = rows.ToList().FindIndex(r => !r.IsUpdate);
        Assert.All(rows.Skip(firstNonUpdateIndex), r => Assert.False(r.IsUpdate));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test --filter ReportMergerTests`
Expected: FAIL; `ReportMerger` does not exist.

- [ ] **Step 4: Write the implementation**

`src/WinUpdateChecker.Core/Matching/ReportMerger.cs`:

```csharp
using System.Text.RegularExpressions;
using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Matching;

/// <summary>
/// Merges registry-installed programs with package-manager upgrade candidates,
/// ported from v1 Merge-ProgramsAndUpgrades / Resolve-DuplicatePackageRows.
/// Match order: exact name -> normalized base prefix -> word-boundary containment.
/// </summary>
public static class ReportMerger
{
    public static IReadOnlyList<ReportRow> Merge(
        IReadOnlyList<InstalledProgram> programs,
        IReadOnlyList<UpgradeCandidate> upgrades,
        IReadOnlyList<string> enabledSources)
    {
        var rows = new List<ReportRow>();
        var matchedKeys = new HashSet<string>();

        foreach (var prog in programs)
        {
            var match = FindMatch(prog, upgrades);
            if (match is not null)
            {
                matchedKeys.Add($"{match.PackageSource}|{match.Id}");
                // Only flag an update when the available version is actually newer than
                // what's installed. This stops phantom "updates" where winget keys off an
                // older wrapper entry than the runtime you already have.
                var isNewer = VersionLogic.IsNewerVersion(prog.Version, match.Available);
                rows.Add(new ReportRow(
                    Name: prog.Name,
                    Publisher: prog.Publisher,
                    Current: prog.Version,
                    Available: isNewer ? match.Available : "",
                    Status: isNewer ? "Update available" : "Up to date / unknown",
                    PackageId: isNewer ? match.Id : "",
                    PackageSource: isNewer ? match.PackageSource : ""));
            }
            else
            {
                var status = enabledSources.Count > 0 ? "Up to date / unknown" : "No package manager detected";
                rows.Add(new ReportRow(prog.Name, prog.Publisher, prog.Version, "", status, "", ""));
            }
        }

        foreach (var up in upgrades)
        {
            if (matchedKeys.Contains($"{up.PackageSource}|{up.Id}")) continue;
            rows.Add(new ReportRow(up.Name, "", up.Current, up.Available,
                $"Update available ({up.PackageSource} only)", up.Id, up.PackageSource));
        }

        return ResolveDuplicatePackageRows(rows)
            .OrderBy(r => r.IsUpdate ? 0 : 1)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static UpgradeCandidate? FindMatch(InstalledProgram prog, IReadOnlyList<UpgradeCandidate> upgrades)
    {
        foreach (var up in upgrades)
            if (string.Equals(up.Name, prog.Name, StringComparison.OrdinalIgnoreCase))
                return up;

        var progBase = NameNormalizer.GetMatchBase(prog.Name);
        foreach (var up in upgrades)
        {
            if (string.IsNullOrEmpty(up.Name)) continue;
            if (NameNormalizer.IsBasePrefixMatch(progBase, NameNormalizer.GetMatchBase(up.Name)))
                return up;
        }

        var progLower = prog.Name.ToLowerInvariant();
        foreach (var up in upgrades)
        {
            if (string.IsNullOrEmpty(up.Name)) continue;
            var upLower = up.Name.ToLowerInvariant();
            if (Regex.IsMatch(progLower, $@"\b{Regex.Escape(upLower)}\b")
                || Regex.IsMatch(upLower, $@"\b{Regex.Escape(progLower)}\b"))
                return up;
        }
        return null;
    }

    /// <summary>
    /// Collapse multiple installed entries that map to the same package id into a single
    /// "Update available" row (keeping the highest installed version), so stale leftover
    /// registry entries don't show as separate phantom updates.
    /// </summary>
    private static List<ReportRow> ResolveDuplicatePackageRows(List<ReportRow> rows)
    {
        var kept = new Dictionary<string, ReportRow>();
        var result = new List<ReportRow>();
        foreach (var r in rows)
        {
            if (!r.IsUpdate || string.IsNullOrWhiteSpace(r.PackageId))
            {
                result.Add(r);
                continue;
            }
            var key = $"{r.PackageSource}|{r.PackageId}";
            if (!kept.TryGetValue(key, out var existing))
            {
                kept[key] = r;
                result.Add(r);
            }
            else if (VersionLogic.GetVersionValue(r.Current) > VersionLogic.GetVersionValue(existing.Current))
            {
                result.Remove(existing);
                result.Add(r);
                kept[key] = r;
            }
        }
        return result;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter ReportMergerTests`
Expected: PASS (9 tests). Also run the full suite (`dotnet test`): everything still green.

- [ ] **Step 6: Commit**

```powershell
git add src/WinUpdateChecker.Core/Models tests/WinUpdateChecker.Core.Tests/Matching/ReportMergerTests.cs src/WinUpdateChecker.Core/Matching/ReportMerger.cs
git commit -m "feat(core): port merge logic with phantom-row suppression"
```

---

### Task 7: Winget output parser

Ports the column-position parsing from `Get-WingetUpgrades` (UpdateChecker.ps1:142-192) as a pure function.

**Files:**
- Create: `src/WinUpdateChecker.Core/Sources/WingetOutputParser.cs`
- Test: `tests/WinUpdateChecker.Core.Tests/Sources/WingetOutputParserTests.cs`

- [ ] **Step 1: Write the failing tests**

The fixture mimics real winget output. **Column alignment matters**: the parser is position-based, so keep the fixture columns exactly aligned with the header words.

```csharp
using WinUpdateChecker.Core.Sources;

namespace WinUpdateChecker.Core.Tests.Sources;

public class WingetOutputParserTests
{
    // The parser slices by header column positions, so the fixture is built with PadRight
    // to guarantee data cells start exactly under their header words.
    private static string Row(string name, string id, string version, string available, string source)
        => name.PadRight(52) + id.PadRight(33) + version.PadRight(15) + available.PadRight(15) + source;

    private static readonly string Fixture = string.Join('\n',
        Row("Name", "Id", "Version", "Available", "Source"),
        new string('-', 120),
        Row("Microsoft Visual C++ 2015-2022 Redistributable (…", "Microsoft.VCRedist.2015+.x64", "14.44.35211.0", "14.51.36231.0", "winget"),
        Row("Git", "Git.Git", "2.44.0", "2.45.2", "winget"),
        "2 upgrades available.",
        "");

    [Fact]
    public void Parse_ExtractsAllUpgradeRows()
    {
        var result = WingetOutputParser.Parse(Fixture);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Parse_ExtractsColumnsByPosition()
    {
        var git = WingetOutputParser.Parse(Fixture)[1];
        Assert.Equal("Git", git.Name);
        Assert.Equal("Git.Git", git.Id);
        Assert.Equal("2.44.0", git.Current);
        Assert.Equal("2.45.2", git.Available);
        Assert.Equal("winget", git.PackageSource);
    }

    [Fact]
    public void Parse_PreservesEllipsisTruncatedNames()
    {
        var vc = WingetOutputParser.Parse(Fixture)[0];
        Assert.Equal("Microsoft Visual C++ 2015-2022 Redistributable (…", vc.Name);
        Assert.Equal("Microsoft.VCRedist.2015+.x64", vc.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n  \n")]
    [InlineData("No installed package found matching input criteria.\n")] // no header line
    public void Parse_ReturnsEmptyOnNonTableOutput(string raw)
        => Assert.Empty(WingetOutputParser.Parse(raw));

    [Fact]
    public void Parse_SkipsMalformedShortLines()
    {
        var raw = Fixture.Replace("2 upgrades available.\n", "x\n2 upgrades available.\n");
        Assert.Equal(2, WingetOutputParser.Parse(raw).Count);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter WingetOutputParserTests`
Expected: FAIL; `WingetOutputParser` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
using System.Text.RegularExpressions;
using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Sources;

/// <summary>
/// Parses `winget upgrade` table output by header column positions,
/// ported from v1 Get-WingetUpgrades.
/// </summary>
public static partial class WingetOutputParser
{
    public static IReadOnlyList<UpgradeCandidate> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        var lines = raw.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

        var headerIndex = Array.FindIndex(lines, l => HeaderPattern().IsMatch(l));
        if (headerIndex < 0) return [];

        var header = lines[headerIndex];
        var idCol = header.IndexOf("Id", StringComparison.Ordinal);
        var versionCol = header.IndexOf("Version", StringComparison.Ordinal);
        var availableCol = header.IndexOf("Available", StringComparison.Ordinal);
        var sourceCol = header.IndexOf("Source", StringComparison.Ordinal);

        var upgrades = new List<UpgradeCandidate>();
        for (var i = headerIndex + 2; i < lines.Length; i++) // +2 skips the dashed separator
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (FooterPattern().IsMatch(line)) break;
            if (SeparatorPattern().IsMatch(line)) continue;
            if (line.Length < availableCol) continue;

            string name, id, current, available;
            try
            {
                name = line[..idCol].Trim();
                id = line[idCol..versionCol].Trim();
                current = line[versionCol..availableCol].Trim();
                available = sourceCol > 0 && line.Length >= sourceCol
                    ? line[availableCol..sourceCol].Trim()
                    : line[availableCol..].Trim();
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id)) continue;
            upgrades.Add(new UpgradeCandidate(name, id, current, available, "winget"));
        }
        return upgrades;
    }

    [GeneratedRegex(@"^Name\s+Id\s+Version\s+Available")]
    private static partial Regex HeaderPattern();

    [GeneratedRegex(@"^\s*\d+\s+upgrades available")]
    private static partial Regex FooterPattern();

    [GeneratedRegex(@"^\s*[-]+\s*$")]
    private static partial Regex SeparatorPattern();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter WingetOutputParserTests`
Expected: PASS. If a column test fails, check fixture alignment first; the header word positions define the slice boundaries.

- [ ] **Step 5: Commit**

```powershell
git add src/WinUpdateChecker.Core/Sources/WingetOutputParser.cs tests/WinUpdateChecker.Core.Tests/Sources/WingetOutputParserTests.cs
git commit -m "feat(core): port winget output parser"
```

---

### Task 8: Scoop output parser

Ports `Get-ScoopUpgrades` parsing (UpdateChecker.ps1:196-240).

**Files:**
- Create: `src/WinUpdateChecker.Core/Sources/ScoopOutputParser.cs`
- Test: `tests/WinUpdateChecker.Core.Tests/Sources/ScoopOutputParserTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using WinUpdateChecker.Core.Sources;

namespace WinUpdateChecker.Core.Tests.Sources;

public class ScoopOutputParserTests
{
    private const string Fixture =
        "Name    Installed Version  Latest Version     Missing Dependencies  Info\n" +
        "----    -----------------  --------------     --------------------  ----\n" +
        "7zip    23.01              24.08\n" +
        "git     2.44.0             2.45.2             \n" +
        "neovim  0.9.5              0.9.5\n";  // current == latest -> not an upgrade

    [Fact]
    public void Parse_ExtractsOnlyRowsWithNewerVersions()
    {
        var result = ScoopOutputParser.Parse(Fixture);
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, u => u.Name == "neovim");
    }

    [Fact]
    public void Parse_UsesNameAsId_AndTagsScoopSource()
    {
        var git = ScoopOutputParser.Parse(Fixture).First(u => u.Name == "git");
        Assert.Equal("git", git.Id);
        Assert.Equal("2.44.0", git.Current);
        Assert.Equal("2.45.2", git.Available);
        Assert.Equal("scoop", git.PackageSource);
    }

    [Fact]
    public void Parse_StripsTrailingInfoFromLatestVersion()
    {
        // "Latest Version" cell can carry trailing columns; only the first token counts.
        var raw =
            "Name    Installed Version  Latest Version     Missing Dependencies  Info\n" +
            "----    -----------------  --------------     --------------------  ----\n" +
            "vlc     3.0.20             3.0.21             Held package\n";
        var vlc = ScoopOutputParser.Parse(raw).Single();
        Assert.Equal("3.0.21", vlc.Available);
    }

    [Theory]
    [InlineData("")]
    [InlineData("WARN  Scoop update check skipped\n")]
    public void Parse_ReturnsEmptyOnNonTableOutput(string raw)
        => Assert.Empty(ScoopOutputParser.Parse(raw));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter ScoopOutputParserTests`
Expected: FAIL; `ScoopOutputParser` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
using System.Text.RegularExpressions;
using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Sources;

/// <summary>Parses `scoop status` table output, ported from v1 Get-ScoopUpgrades.</summary>
public static partial class ScoopOutputParser
{
    public static IReadOnlyList<UpgradeCandidate> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        var lines = raw.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

        var headerIndex = Array.FindIndex(lines, l => HeaderPattern().IsMatch(l));
        if (headerIndex < 0) return [];

        var header = lines[headerIndex];
        var instCol = header.IndexOf("Installed Version", StringComparison.Ordinal);
        var latestCol = header.IndexOf("Latest Version", StringComparison.Ordinal);

        var upgrades = new List<UpgradeCandidate>();
        for (var i = headerIndex + 2; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (SeparatorPattern().IsMatch(line)) continue;
            if (line.Length < latestCol) continue;

            string name, current, available;
            try
            {
                name = line[..instCol].Trim();
                current = line[instCol..latestCol].Trim();
                available = TrailingColumns().Replace(line[latestCol..].Trim(), "");
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(name)) continue;
            if (string.IsNullOrWhiteSpace(available)) continue;
            if (current == available) continue;

            upgrades.Add(new UpgradeCandidate(name, name, current, available, "scoop"));
        }
        return upgrades;
    }

    [GeneratedRegex(@"^Name\s+Installed Version\s+Latest Version")]
    private static partial Regex HeaderPattern();

    [GeneratedRegex(@"^\s*[-]+\s*$")]
    private static partial Regex SeparatorPattern();

    [GeneratedRegex(@"\s+.*$")]
    private static partial Regex TrailingColumns();
}
```

**Note:** scoop's dashed separator line sits directly under the header and is skipped by construction; the data loop starts at `headerIndex + 2`, exactly like v1. The `SeparatorPattern` check only guards against stray pure-dash lines further down.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter ScoopOutputParserTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/WinUpdateChecker.Core/Sources/ScoopOutputParser.cs tests/WinUpdateChecker.Core.Tests/Sources/ScoopOutputParserTests.cs
git commit -m "feat(core): port scoop output parser"
```

---

### Task 9: Chocolatey output parser

Ports `Get-ChocoUpgrades` parsing (UpdateChecker.ps1:244-265).

**Files:**
- Create: `src/WinUpdateChecker.Core/Sources/ChocoOutputParser.cs`
- Test: `tests/WinUpdateChecker.Core.Tests/Sources/ChocoOutputParserTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using WinUpdateChecker.Core.Sources;

namespace WinUpdateChecker.Core.Tests.Sources;

public class ChocoOutputParserTests
{
    // `choco outdated -r` emits pipe-delimited rows: name|current|available|pinned
    private const string Fixture =
        "7zip|23.1.0|24.8.0|false\n" +
        "git.install|2.44.0|2.45.2|false\n";

    [Fact]
    public void Parse_ExtractsPipeDelimitedRows()
    {
        var result = ChocoOutputParser.Parse(Fixture);
        Assert.Equal(2, result.Count);
        var git = result[1];
        Assert.Equal("git.install", git.Name);
        Assert.Equal("git.install", git.Id);
        Assert.Equal("2.44.0", git.Current);
        Assert.Equal("2.45.2", git.Available);
        Assert.Equal("chocolatey", git.PackageSource);
    }

    [Fact]
    public void Parse_SkipsLinesWithTooFewFields()
    {
        var result = ChocoOutputParser.Parse("garbage line\nok|1.0|2.0|false\n");
        Assert.Single(result);
        Assert.Equal("ok", result[0].Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n\n")]
    public void Parse_ReturnsEmptyOnEmptyOutput(string raw)
        => Assert.Empty(ChocoOutputParser.Parse(raw));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter ChocoOutputParserTests`
Expected: FAIL; `ChocoOutputParser` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Sources;

/// <summary>Parses `choco outdated -r` pipe-delimited output, ported from v1 Get-ChocoUpgrades.</summary>
public static class ChocoOutputParser
{
    public static IReadOnlyList<UpgradeCandidate> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        var upgrades = new List<UpgradeCandidate>();
        foreach (var rawLine in raw.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split('|');
            if (parts.Length < 3) continue;
            var name = parts[0].Trim();
            upgrades.Add(new UpgradeCandidate(name, name, parts[1].Trim(), parts[2].Trim(), "chocolatey"));
        }
        return upgrades;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter ChocoOutputParserTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/WinUpdateChecker.Core/Sources/ChocoOutputParser.cs tests/WinUpdateChecker.Core.Tests/Sources/ChocoOutputParserTests.cs
git commit -m "feat(core): port chocolatey output parser"
```

---

### Task 10: Process runner

The single seam through which all package-manager CLIs are invoked. Thin OS wrapper; only `CommandExists` gets a unit test; `RunAsync`/`RunElevatedAsync` are exercised end-to-end in the Task 17 smoke test.

**Files:**
- Create: `src/WinUpdateChecker.Core/Sources/IProcessRunner.cs`, `src/WinUpdateChecker.Core/Sources/ProcessRunner.cs`
- Test: `tests/WinUpdateChecker.Core.Tests/Sources/ProcessRunnerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using WinUpdateChecker.Core.Sources;

namespace WinUpdateChecker.Core.Tests.Sources;

public class ProcessRunnerTests
{
    [Fact]
    public void CommandExists_FindsCmd()
        => Assert.True(new ProcessRunner().CommandExists("cmd"));

    [Fact]
    public void CommandExists_RejectsNonexistentCommand()
        => Assert.False(new ProcessRunner().CommandExists("definitely-not-a-real-command-xyz"));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter ProcessRunnerTests`
Expected: FAIL; `ProcessRunner` does not exist.

- [ ] **Step 3: Write the interface and implementation**

`src/WinUpdateChecker.Core/Sources/IProcessRunner.cs`:

```csharp
namespace WinUpdateChecker.Core.Sources;

public sealed record ProcessResult(int? ExitCode, string StdOut, string StdErr);

/// <summary>Seam for invoking package-manager CLIs; mocked in tests.</summary>
public interface IProcessRunner
{
    bool CommandExists(string command);

    /// <summary>Run hidden with stdout/stderr captured.</summary>
    Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct = default);

    /// <summary>Run elevated via UAC. ShellExecute cannot redirect output, so only the exit code is captured.</summary>
    Task<ProcessResult> RunElevatedAsync(string fileName, string arguments, CancellationToken ct = default);
}
```

`src/WinUpdateChecker.Core/Sources/ProcessRunner.cs`:

```csharp
using System.Diagnostics;
using System.Text;

namespace WinUpdateChecker.Core.Sources;

public sealed class ProcessRunner : IProcessRunner
{
    public bool CommandExists(string command)
    {
        var pathExt = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM")
            .Split(';', StringSplitOptions.RemoveEmptyEntries);
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var dir in paths)
        {
            foreach (var ext in pathExt)
            {
                try
                {
                    if (File.Exists(Path.Combine(dir.Trim(), command + ext))) return true;
                }
                catch (ArgumentException)
                {
                    // malformed PATH entry; skip it
                }
            }
        }
        return false;
    }

    public async Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {fileName}");
        var stdOut = proc.StandardOutput.ReadToEndAsync(ct);
        var stdErr = proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        return new ProcessResult(proc.ExitCode, await stdOut, await stdErr);
    }

    public async Task<ProcessResult> RunElevatedAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = true, // required for the UAC verb
            Verb = "runas",
        };
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {fileName}");
        await proc.WaitForExitAsync(ct);
        return new ProcessResult(proc.ExitCode, "", "");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter ProcessRunnerTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/WinUpdateChecker.Core/Sources/IProcessRunner.cs src/WinUpdateChecker.Core/Sources/ProcessRunner.cs tests/WinUpdateChecker.Core.Tests/Sources/ProcessRunnerTests.cs
git commit -m "feat(core): add process runner seam for package-manager CLIs"
```

---

### Task 11: Package sources

`IPackageSource` plus the three implementations. Each wires the parser to the runner and knows its exact upgrade command line (ported from `Invoke-PackageUpgrade`, UpdateChecker.ps1:465-509).

**Files:**
- Create: `src/WinUpdateChecker.Core/Sources/IPackageSource.cs`, `src/WinUpdateChecker.Core/Sources/WingetSource.cs`, `src/WinUpdateChecker.Core/Sources/ScoopSource.cs`, `src/WinUpdateChecker.Core/Sources/ChocoSource.cs`
- Test: `tests/WinUpdateChecker.Core.Tests/Sources/PackageSourceTests.cs`, `tests/WinUpdateChecker.Core.Tests/Sources/FakeProcessRunner.cs`

- [ ] **Step 1: Write the fake runner (shared by this task and Tasks 12/14)**

`tests/WinUpdateChecker.Core.Tests/Sources/FakeProcessRunner.cs`:

```csharp
using WinUpdateChecker.Core.Sources;

namespace WinUpdateChecker.Core.Tests.Sources;

public sealed class FakeProcessRunner : IProcessRunner
{
    public List<(string FileName, string Arguments, bool Elevated)> Calls { get; } = [];
    public string StdOut { get; set; } = "";
    public int ExitCode { get; set; }
    public bool Exists { get; set; } = true;
    public Exception? ThrowOnRun { get; set; }

    public bool CommandExists(string command) => Exists;

    public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        if (ThrowOnRun is not null) throw ThrowOnRun;
        Calls.Add((fileName, arguments, false));
        return Task.FromResult(new ProcessResult(ExitCode, StdOut, ""));
    }

    public Task<ProcessResult> RunElevatedAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        if (ThrowOnRun is not null) throw ThrowOnRun;
        Calls.Add((fileName, arguments, true));
        return Task.FromResult(new ProcessResult(ExitCode, "", ""));
    }
}
```

- [ ] **Step 2: Write the failing source tests**

```csharp
using WinUpdateChecker.Core.Sources;

namespace WinUpdateChecker.Core.Tests.Sources;

public class PackageSourceTests
{
    [Fact]
    public async Task Winget_ListOutdated_UsesExactArguments()
    {
        var runner = new FakeProcessRunner();
        await new WingetSource(runner).ListOutdatedAsync();
        var call = Assert.Single(runner.Calls);
        Assert.Equal("winget", call.FileName);
        Assert.Equal("upgrade --include-unknown --accept-source-agreements", call.Arguments);
        Assert.False(call.Elevated);
    }

    [Fact]
    public async Task Winget_Upgrade_IsElevatedWithExactIdMatch()
    {
        var runner = new FakeProcessRunner();
        await new WingetSource(runner).RunUpgradeAsync("Git.Git");
        var call = Assert.Single(runner.Calls);
        Assert.Equal("winget", call.FileName);
        Assert.Equal(
            "upgrade --id Git.Git --exact --source winget --accept-package-agreements --accept-source-agreements --disable-interactivity -h",
            call.Arguments);
        Assert.True(call.Elevated);
    }

    [Fact]
    public async Task Scoop_Upgrade_RunsAsCurrentUserThroughPowerShell()
    {
        // The id is interpolated into a child-shell command, so callers MUST validate it
        // with PackageIdValidator first (enforced by UpgradeRunner in the next task).
        var runner = new FakeProcessRunner();
        await new ScoopSource(runner).RunUpgradeAsync("extras-app_name");
        var call = Assert.Single(runner.Calls);
        Assert.Equal("powershell", call.FileName);
        Assert.Equal("-NoProfile -Command \"scoop update extras-app_name\"", call.Arguments);
        Assert.False(call.Elevated);
    }

    [Fact]
    public async Task Choco_Upgrade_IsElevated()
    {
        var runner = new FakeProcessRunner();
        await new ChocoSource(runner).RunUpgradeAsync("git.install");
        var call = Assert.Single(runner.Calls);
        Assert.Equal("choco", call.FileName);
        Assert.Equal("upgrade git.install -y", call.Arguments);
        Assert.True(call.Elevated);
    }

    [Fact]
    public async Task ListOutdated_FeedsRawOutputToParser()
    {
        var runner = new FakeProcessRunner { StdOut = "7zip|23.1.0|24.8.0|false\n" };
        var result = await new ChocoSource(runner).ListOutdatedAsync();
        Assert.Single(result);
        Assert.Equal("7zip", result[0].Id);
    }

    [Theory]
    [InlineData("winget", "winget")]
    [InlineData("scoop", "scoop")]
    [InlineData("chocolatey", "choco")]
    public void IsInstalled_ChecksTheRightExecutable(string sourceName, string expectedExe)
    {
        var runner = new FakeProcessRunner();
        IPackageSource source = sourceName switch
        {
            "winget" => new WingetSource(runner),
            "scoop" => new ScoopSource(runner),
            _ => new ChocoSource(runner),
        };
        Assert.Equal(sourceName, source.Name);
        Assert.True(source.IsInstalled());
        // FakeProcessRunner can't observe the exe name for CommandExists, so assert the
        // contract on the real constant instead:
        Assert.Equal(expectedExe, source.ExecutableName);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test --filter PackageSourceTests`
Expected: FAIL; `IPackageSource` and the source classes do not exist.

- [ ] **Step 4: Write the interface and implementations**

`src/WinUpdateChecker.Core/Sources/IPackageSource.cs`:

```csharp
using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Sources;

/// <summary>A package manager that can report and apply upgrades.</summary>
public interface IPackageSource
{
    /// <summary>Canonical source name: "winget", "scoop", or "chocolatey".</summary>
    string Name { get; }

    /// <summary>The executable probed on PATH to detect the manager.</summary>
    string ExecutableName { get; }

    bool IsInstalled();

    /// <summary>Query the manager for outdated packages. Exceptions propagate; ScanService turns them into warnings.</summary>
    Task<IReadOnlyList<UpgradeCandidate>> ListOutdatedAsync(CancellationToken ct = default);

    /// <summary>
    /// Invoke the manager's upgrade command for a package id. Callers MUST validate the id
    /// with <see cref="Upgrading.PackageIdValidator"/> first; UpgradeRunner enforces this.
    /// </summary>
    Task<ProcessResult> RunUpgradeAsync(string packageId, CancellationToken ct = default);
}
```

`src/WinUpdateChecker.Core/Sources/WingetSource.cs`:

```csharp
using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Sources;

public sealed class WingetSource(IProcessRunner runner) : IPackageSource
{
    public string Name => "winget";
    public string ExecutableName => "winget";

    public bool IsInstalled() => runner.CommandExists(ExecutableName);

    public async Task<IReadOnlyList<UpgradeCandidate>> ListOutdatedAsync(CancellationToken ct = default)
    {
        var result = await runner.RunAsync("winget", "upgrade --include-unknown --accept-source-agreements", ct);
        return WingetOutputParser.Parse(result.StdOut);
    }

    public Task<ProcessResult> RunUpgradeAsync(string packageId, CancellationToken ct = default)
        => runner.RunElevatedAsync("winget",
            $"upgrade --id {packageId} --exact --source winget --accept-package-agreements --accept-source-agreements --disable-interactivity -h", ct);
}
```

`src/WinUpdateChecker.Core/Sources/ScoopSource.cs`:

```csharp
using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Sources;

public sealed class ScoopSource(IProcessRunner runner) : IPackageSource
{
    public string Name => "scoop";
    public string ExecutableName => "scoop";

    public bool IsInstalled() => runner.CommandExists(ExecutableName);

    public async Task<IReadOnlyList<UpgradeCandidate>> ListOutdatedAsync(CancellationToken ct = default)
    {
        var result = await runner.RunAsync("scoop", "status", ct);
        return ScoopOutputParser.Parse(result.StdOut);
    }

    // Scoop runs as the current user (no elevation). The id is interpolated into a
    // child-shell command, so it MUST stay constrained to the safe id charset enforced
    // by PackageIdValidator (UpgradeRunner refuses unsafe ids before reaching here).
    public Task<ProcessResult> RunUpgradeAsync(string packageId, CancellationToken ct = default)
        => runner.RunAsync("powershell", $"-NoProfile -Command \"scoop update {packageId}\"", ct);
}
```

`src/WinUpdateChecker.Core/Sources/ChocoSource.cs`:

```csharp
using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Sources;

public sealed class ChocoSource(IProcessRunner runner) : IPackageSource
{
    public string Name => "chocolatey";
    public string ExecutableName => "choco";

    public bool IsInstalled() => runner.CommandExists(ExecutableName);

    public async Task<IReadOnlyList<UpgradeCandidate>> ListOutdatedAsync(CancellationToken ct = default)
    {
        var result = await runner.RunAsync("choco", "outdated -r --no-color", ct);
        return ChocoOutputParser.Parse(result.StdOut);
    }

    public Task<ProcessResult> RunUpgradeAsync(string packageId, CancellationToken ct = default)
        => runner.RunElevatedAsync("choco", $"upgrade {packageId} -y", ct);
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter PackageSourceTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/WinUpdateChecker.Core/Sources tests/WinUpdateChecker.Core.Tests/Sources
git commit -m "feat(core): add winget/scoop/chocolatey package sources"
```

---

### Task 12: Upgrade runner

Central upgrade dispatch: id validation (refuse before any process launch), exit-code translation, exception capture. Ports `Invoke-PackageUpgrade`'s orchestration.

**Files:**
- Create: `src/WinUpdateChecker.Core/Upgrading/UpgradeRunner.cs`, `src/WinUpdateChecker.Core/Upgrading/UpgradeResult.cs`
- Test: `tests/WinUpdateChecker.Core.Tests/Upgrading/UpgradeRunnerTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using WinUpdateChecker.Core.Sources;
using WinUpdateChecker.Core.Tests.Sources;
using WinUpdateChecker.Core.Upgrading;

namespace WinUpdateChecker.Core.Tests.Upgrading;

public class UpgradeRunnerTests
{
    private static UpgradeRunner CreateRunner(FakeProcessRunner fake)
        => new([new WingetSource(fake), new ScoopSource(fake), new ChocoSource(fake)]);

    [Fact]
    public async Task RefusesCraftedId_WithoutLaunchingAnyProcess()
    {
        // Ports the Pester case: Invoke-PackageUpgrade blocks crafted id.
        var fake = new FakeProcessRunner();
        var result = await CreateRunner(fake).UpgradeAsync("scoop", "evil; calc.exe", "evil");
        Assert.False(result.Success);
        Assert.StartsWith("Refused:", result.Message);
        Assert.Empty(fake.Calls); // nothing was executed
    }

    [Fact]
    public async Task UnknownSource_FailsCleanly()
    {
        var result = await CreateRunner(new FakeProcessRunner()).UpgradeAsync("npm", "left-pad", "left-pad");
        Assert.False(result.Success);
        Assert.Contains("Unknown package source", result.Message);
    }

    [Fact]
    public async Task ExitZero_IsSuccess()
    {
        var fake = new FakeProcessRunner { ExitCode = 0 };
        var result = await CreateRunner(fake).UpgradeAsync("winget", "Git.Git", "Git");
        Assert.True(result.Success);
        Assert.Equal("Succeeded.", result.Message);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task NonZeroExit_IsTranslatedFailure()
    {
        var fake = new FakeProcessRunner { ExitCode = -1978335189 };
        var result = await CreateRunner(fake).UpgradeAsync("winget", "Git.Git", "Git");
        Assert.False(result.Success);
        Assert.StartsWith("No applicable upgrade", result.Message);
    }

    [Fact]
    public async Task LaunchException_BecomesFailureMessage()
    {
        // Most commonly the user declined the UAC elevation prompt.
        var fake = new FakeProcessRunner { ThrowOnRun = new InvalidOperationException("The operation was canceled by the user.") };
        var result = await CreateRunner(fake).UpgradeAsync("winget", "Git.Git", "Git");
        Assert.False(result.Success);
        Assert.Equal("The operation was canceled by the user.", result.Message);
    }

    [Fact]
    public async Task CapturedOutput_BecomesLog()
    {
        var fake = new FakeProcessRunner { StdOut = "Updating extras-app_name...\nDone." };
        var result = await CreateRunner(fake).UpgradeAsync("scoop", "extras-app_name", "App");
        Assert.Contains("Updating extras-app_name", result.Log);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter UpgradeRunnerTests`
Expected: FAIL; `UpgradeRunner` / `UpgradeResult` do not exist.

- [ ] **Step 3: Write the implementation**

`src/WinUpdateChecker.Core/Upgrading/UpgradeResult.cs`:

```csharp
namespace WinUpdateChecker.Core.Upgrading;

public sealed record UpgradeResult(
    string Name,
    string Id,
    string Source,
    bool Success,
    int? ExitCode,
    string Message,
    string Log);
```

`src/WinUpdateChecker.Core/Upgrading/UpgradeRunner.cs`:

```csharp
using WinUpdateChecker.Core.Sources;

namespace WinUpdateChecker.Core.Upgrading;

/// <summary>
/// Dispatches an upgrade to the owning package source. Validates the package id BEFORE
/// any process is launched (ported v1 security fix b95dca8) and translates exit codes
/// into human-readable messages.
/// </summary>
public sealed class UpgradeRunner(IEnumerable<IPackageSource> sources)
{
    private readonly Dictionary<string, IPackageSource> _sources =
        sources.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

    public async Task<UpgradeResult> UpgradeAsync(string source, string packageId, string displayName, CancellationToken ct = default)
    {
        if (!PackageIdValidator.IsSafe(packageId))
            return new UpgradeResult(displayName, packageId, source, false, null,
                "Refused: package id contains unexpected characters.", "");

        if (!_sources.TryGetValue(source, out var src))
            return new UpgradeResult(displayName, packageId, source, false, null,
                $"Unknown package source '{source}'.", "");

        try
        {
            var proc = await src.RunUpgradeAsync(packageId, ct);
            var log = string.Join('\n', new[] { proc.StdOut, proc.StdErr }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
            return new UpgradeResult(displayName, packageId, source,
                proc.ExitCode == 0, proc.ExitCode,
                UpgradeExitMessages.Translate(source, proc.ExitCode), log);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Most commonly the user declined the UAC elevation prompt.
            return new UpgradeResult(displayName, packageId, source, false, null, ex.Message, "");
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter UpgradeRunnerTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/WinUpdateChecker.Core/Upgrading tests/WinUpdateChecker.Core.Tests/Upgrading/UpgradeRunnerTests.cs
git commit -m "feat(core): add upgrade dispatch with id validation"
```

---

### Task 13: Registry scanner

Ports `Get-InstalledPrograms` (UpdateChecker.ps1:92-138). The filter rules are extracted into a pure, tested function; the registry walk itself is a thin wrapper.

**Files:**
- Create: `src/WinUpdateChecker.Core/Scanning/RegistryEntry.cs`, `src/WinUpdateChecker.Core/Scanning/RegistryEntryFilter.cs`, `src/WinUpdateChecker.Core/Scanning/IInstalledProgramProvider.cs`, `src/WinUpdateChecker.Core/Scanning/RegistryScanner.cs`
- Test: `tests/WinUpdateChecker.Core.Tests/Scanning/RegistryEntryFilterTests.cs`

- [ ] **Step 1: Write the failing filter tests**

```csharp
using WinUpdateChecker.Core.Scanning;

namespace WinUpdateChecker.Core.Tests.Scanning;

public class RegistryEntryFilterTests
{
    private static RegistryEntry Entry(
        string? name = "Some App", string? version = "1.0",
        int? systemComponent = null, string? parentKeyName = null, string? releaseType = null)
        => new(name, version, "Pub", systemComponent, parentKeyName, releaseType);

    [Fact]
    public void NormalProgram_IsIncluded()
        => Assert.True(RegistryEntryFilter.ShouldInclude(Entry(), includeSystemComponents: false));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingDisplayName_IsAlwaysExcluded(string? name)
    {
        Assert.False(RegistryEntryFilter.ShouldInclude(Entry(name: name), false));
        Assert.False(RegistryEntryFilter.ShouldInclude(Entry(name: name), true)); // even with system components
    }

    [Fact]
    public void SystemComponent_IsExcludedByDefault()
        => Assert.False(RegistryEntryFilter.ShouldInclude(Entry(systemComponent: 1), false));

    [Fact]
    public void ChildEntry_IsExcludedByDefault()
        => Assert.False(RegistryEntryFilter.ShouldInclude(Entry(parentKeyName: "ParentApp"), false));

    [Theory]
    [InlineData("Update")]
    [InlineData("Hotfix")]
    [InlineData("Security Update")]
    public void UpdateReleaseTypes_AreExcludedByDefault(string releaseType)
        => Assert.False(RegistryEntryFilter.ShouldInclude(Entry(releaseType: releaseType), false));

    [Theory]
    [InlineData("KB5034441")]
    [InlineData("Update for Microsoft Office")]
    [InlineData("Security Update for Windows")]
    [InlineData("Hotfix for .NET")]
    public void HotfixStyleNames_AreExcludedByDefault(string name)
        => Assert.False(RegistryEntryFilter.ShouldInclude(Entry(name: name), false));

    [Fact]
    public void IncludeSystemComponents_KeepsEverythingWithAName()
    {
        Assert.True(RegistryEntryFilter.ShouldInclude(Entry(systemComponent: 1), true));
        Assert.True(RegistryEntryFilter.ShouldInclude(Entry(parentKeyName: "x"), true));
        Assert.True(RegistryEntryFilter.ShouldInclude(Entry(name: "KB5034441"), true));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter RegistryEntryFilterTests`
Expected: FAIL; types do not exist.

- [ ] **Step 3: Write the implementation**

`src/WinUpdateChecker.Core/Scanning/RegistryEntry.cs`:

```csharp
namespace WinUpdateChecker.Core.Scanning;

/// <summary>Raw values read from one uninstall registry key.</summary>
public sealed record RegistryEntry(
    string? DisplayName,
    string? DisplayVersion,
    string? Publisher,
    int? SystemComponent,
    string? ParentKeyName,
    string? ReleaseType);
```

`src/WinUpdateChecker.Core/Scanning/RegistryEntryFilter.cs`:

```csharp
using System.Text.RegularExpressions;

namespace WinUpdateChecker.Core.Scanning;

/// <summary>Inclusion rules for uninstall entries, ported from v1 Get-InstalledPrograms.</summary>
public static partial class RegistryEntryFilter
{
    public static bool ShouldInclude(RegistryEntry entry, bool includeSystemComponents)
    {
        if (string.IsNullOrWhiteSpace(entry.DisplayName)) return false;
        if (includeSystemComponents) return true;
        if (entry.SystemComponent == 1) return false;
        if (!string.IsNullOrEmpty(entry.ParentKeyName)) return false;
        if (entry.ReleaseType is "Update" or "Hotfix" or "Security Update") return false;
        if (HotfixStyleName().IsMatch(entry.DisplayName)) return false;
        return true;
    }

    [GeneratedRegex(@"^(KB\d+|Update for|Security Update|Hotfix)")]
    private static partial Regex HotfixStyleName();
}
```

`src/WinUpdateChecker.Core/Scanning/IInstalledProgramProvider.cs`:

```csharp
using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Scanning;

/// <summary>Seam so ScanService can be tested without touching the real registry.</summary>
public interface IInstalledProgramProvider
{
    IReadOnlyList<InstalledProgram> GetInstalledPrograms(bool includeSystemComponents);
}
```

`src/WinUpdateChecker.Core/Scanning/RegistryScanner.cs`:

```csharp
using Microsoft.Win32;
using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Scanning;

/// <summary>
/// Reads installed programs from the HKLM/HKCU uninstall keys (including WOW6432Node),
/// ported from v1 Get-InstalledPrograms. Thin registry walk; the filter rules live in
/// <see cref="RegistryEntryFilter"/> where they are unit-tested.
/// </summary>
public sealed class RegistryScanner : IInstalledProgramProvider
{
    private static readonly (RegistryKey Hive, string Path)[] UninstallKeys =
    [
        (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
        (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
    ];

    public IReadOnlyList<InstalledProgram> GetInstalledPrograms(bool includeSystemComponents)
    {
        var programs = new List<InstalledProgram>();
        var seen = new HashSet<string>();

        foreach (var (hive, path) in UninstallKeys)
        {
            using var key = hive.OpenSubKey(path);
            if (key is null) continue;

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                using var sub = key.OpenSubKey(subKeyName);
                if (sub is null) continue;

                var entry = new RegistryEntry(
                    DisplayName: sub.GetValue("DisplayName") as string,
                    DisplayVersion: sub.GetValue("DisplayVersion") as string,
                    Publisher: sub.GetValue("Publisher") as string,
                    SystemComponent: sub.GetValue("SystemComponent") as int?,
                    ParentKeyName: sub.GetValue("ParentKeyName") as string,
                    ReleaseType: sub.GetValue("ReleaseType") as string);

                if (!RegistryEntryFilter.ShouldInclude(entry, includeSystemComponents)) continue;

                var name = entry.DisplayName!.Trim();
                var version = (entry.DisplayVersion ?? "").Trim();
                if (!seen.Add($"{name}|{version}".ToLowerInvariant())) continue;

                programs.Add(new InstalledProgram(name, version, entry.Publisher));
            }
        }

        return programs.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter RegistryEntryFilterTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/WinUpdateChecker.Core/Scanning tests/WinUpdateChecker.Core.Tests/Scanning
git commit -m "feat(core): port registry scanner with tested filter rules"
```

---

### Task 14: Scan service

Orchestrates registry + sources concurrently (the v2 speedup over v1's sequential `Get-Report`), converts per-source failures into warnings per the spec's error-handling section.

**Files:**
- Create: `src/WinUpdateChecker.Core/Scanning/ScanService.cs`
- Test: `tests/WinUpdateChecker.Core.Tests/Scanning/ScanServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using WinUpdateChecker.Core.Models;
using WinUpdateChecker.Core.Scanning;
using WinUpdateChecker.Core.Sources;

namespace WinUpdateChecker.Core.Tests.Scanning;

public class ScanServiceTests
{
    private sealed class FakePrograms(params InstalledProgram[] programs) : IInstalledProgramProvider
    {
        public IReadOnlyList<InstalledProgram> GetInstalledPrograms(bool includeSystemComponents) => programs;
    }

    private sealed class FakeSource(string name, bool installed = true,
        IReadOnlyList<UpgradeCandidate>? outdated = null, Exception? throws = null) : IPackageSource
    {
        public string Name => name;
        public string ExecutableName => name;
        public bool IsInstalled() => installed;
        public Task<IReadOnlyList<UpgradeCandidate>> ListOutdatedAsync(CancellationToken ct = default)
            => throws is null
                ? Task.FromResult<IReadOnlyList<UpgradeCandidate>>(outdated ?? [])
                : Task.FromException<IReadOnlyList<UpgradeCandidate>>(throws);
        public Task<ProcessResult> RunUpgradeAsync(string packageId, CancellationToken ct = default)
            => Task.FromResult(new ProcessResult(0, "", ""));
    }

    private static readonly InstalledProgram Chrome = new("Google Chrome", "120.0.0.0", "Google LLC");

    [Fact]
    public async Task Scan_MergesProgramsWithSourceResults()
    {
        var source = new FakeSource("winget",
            outdated: [new UpgradeCandidate("Google Chrome", "Google.Chrome", "120.0.0.0", "125.0.0.0", "winget")]);
        var service = new ScanService(new FakePrograms(Chrome), [source]);

        var result = await service.ScanAsync(ScanOptions.Default);

        Assert.Equal(1, result.ProgramCount);
        Assert.Equal(new[] { "winget" }, result.EnabledSources);
        var row = Assert.Single(result.Rows);
        Assert.True(row.IsUpdate);
        Assert.Equal("Google.Chrome", row.PackageId);
    }

    [Fact]
    public async Task UninstalledSource_IsSkippedNotErrored()
    {
        var service = new ScanService(new FakePrograms(Chrome),
            [new FakeSource("winget"), new FakeSource("scoop", installed: false)]);

        var result = await service.ScanAsync(ScanOptions.Default);

        Assert.Equal(new[] { "winget" }, result.EnabledSources);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task FailingSource_ProducesWarningAndOthersStillMerge()
    {
        var ok = new FakeSource("winget",
            outdated: [new UpgradeCandidate("Google Chrome", "Google.Chrome", "120.0.0.0", "125.0.0.0", "winget")]);
        var broken = new FakeSource("chocolatey", throws: new InvalidOperationException("boom"));
        var service = new ScanService(new FakePrograms(Chrome), [ok, broken]);

        var result = await service.ScanAsync(ScanOptions.Default);

        var warning = Assert.Single(result.Warnings);
        Assert.Contains("chocolatey", warning);
        Assert.Contains("results may be incomplete", warning);
        Assert.True(Assert.Single(result.Rows).IsUpdate); // winget result survived
    }

    [Fact]
    public async Task SourceFilter_RestrictsQueriedSources()
    {
        var service = new ScanService(new FakePrograms(Chrome),
            [new FakeSource("winget"), new FakeSource("scoop")]);

        var result = await service.ScanAsync(new ScanOptions(["scoop"], false));

        Assert.Equal(new[] { "scoop" }, result.EnabledSources);
    }

    [Fact]
    public async Task NoSources_YieldsNoPackageManagerStatus()
    {
        var service = new ScanService(new FakePrograms(Chrome), []);
        var result = await service.ScanAsync(ScanOptions.Default);
        Assert.Equal("No package manager detected", Assert.Single(result.Rows).Status);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter ScanServiceTests`
Expected: FAIL; `ScanService` / `ScanOptions` / `ScanResult` do not exist.

- [ ] **Step 3: Write the implementation**

```csharp
using WinUpdateChecker.Core.Matching;
using WinUpdateChecker.Core.Models;
using WinUpdateChecker.Core.Sources;

namespace WinUpdateChecker.Core.Scanning;

public sealed record ScanOptions(IReadOnlyList<string> Sources, bool IncludeSystemComponents)
{
    /// <summary>All detected sources, no system components.</summary>
    public static ScanOptions Default { get; } = new([], false);
}

public sealed record ScanResult(
    IReadOnlyList<ReportRow> Rows,
    IReadOnlyList<string> EnabledSources,
    int ProgramCount,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Runs a full scan: registry read and all enabled source queries in parallel
/// (v1 ran them sequentially), then merges. A source that fails to query becomes
/// a warning; never a scan failure.
/// </summary>
public sealed class ScanService(IInstalledProgramProvider programProvider, IEnumerable<IPackageSource> sources)
{
    private readonly IReadOnlyList<IPackageSource> _sources = sources.ToList();

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        var enabled = _sources
            .Where(s => options.Sources.Count == 0
                || options.Sources.Contains(s.Name, StringComparer.OrdinalIgnoreCase))
            .Where(s => s.IsInstalled())
            .ToList();

        var programsTask = Task.Run(() => programProvider.GetInstalledPrograms(options.IncludeSystemComponents), ct);

        var queries = enabled
            .Select(s => QuerySourceAsync(s, ct))
            .ToList();

        var programs = await programsTask;
        var results = await Task.WhenAll(queries);

        var upgrades = results.SelectMany(r => r.Upgrades).ToList();
        var warnings = results.Where(r => r.Warning is not null).Select(r => r.Warning!).ToList();
        var enabledNames = enabled.Select(s => s.Name).ToList();

        var rows = ReportMerger.Merge(programs, upgrades, enabledNames);
        return new ScanResult(rows, enabledNames, programs.Count, warnings);
    }

    private static async Task<(IReadOnlyList<UpgradeCandidate> Upgrades, string? Warning)> QuerySourceAsync(
        IPackageSource source, CancellationToken ct)
    {
        try
        {
            return (await source.ListOutdatedAsync(ct), null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ([], $"{source.Name} query failed; results may be incomplete ({ex.Message})");
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter ScanServiceTests`
Expected: PASS. Run the full suite too: `dotnet test`: all green.

- [ ] **Step 5: Commit**

```powershell
git add src/WinUpdateChecker.Core/Scanning/ScanService.cs tests/WinUpdateChecker.Core.Tests/Scanning/ScanServiceTests.cs
git commit -m "feat(core): add parallel scan orchestration with per-source warnings"
```

---

### Task 15: CSV and HTML exporters

Ports `Export-CsvReport` and `Export-HtmlReport` (UpdateChecker.ps1:513-598).

**Files:**
- Create: `src/WinUpdateChecker.Core/Export/CsvExporter.cs`, `src/WinUpdateChecker.Core/Export/HtmlExporter.cs`, `src/WinUpdateChecker.Core/AppInfo.cs`
- Test: `tests/WinUpdateChecker.Core.Tests/Export/ExporterTests.cs`

- [ ] **Step 1: Create AppInfo (constants used by exporters and later by the App)**

`src/WinUpdateChecker.Core/AppInfo.cs`:

```csharp
namespace WinUpdateChecker.Core;

public static class AppInfo
{
    public const string ProductName = "WinUpdateChecker";
    public const string Version = "2.0.0";
    public const string RepoUrl = "https://github.com/adrian3092/win-update-checker";
}
```

- [ ] **Step 2: Write the failing exporter tests**

```csharp
using WinUpdateChecker.Core.Export;
using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Tests.Export;

public class ExporterTests
{
    private static readonly IReadOnlyList<ReportRow> Rows =
    [
        new("Git", "The Git \"Team\", Inc.", "2.44.0", "2.45.2", "Update available", "Git.Git", "winget"),
        new("<script>alert(1)</script>", "Evil, Corp", "1.0", "", "Up to date / unknown", "", ""),
    ];

    [Fact]
    public void Csv_WritesHeaderAndQuotedFields()
    {
        var csv = CsvExporter.Render(Rows);
        var lines = csv.TrimEnd().Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        Assert.Equal("Name,Publisher,Current,Available,Status,PackageId,PackageSource", lines[0]);
        Assert.Equal(3, lines.Length);
        // Comma-containing and quote-containing fields are quoted with doubled quotes:
        Assert.Contains("\"The Git \"\"Team\"\", Inc.\"", lines[1]);
        Assert.Contains("\"Evil, Corp\"", lines[2]);
    }

    [Fact]
    public void Html_EncodesContentAndCountsUpdates()
    {
        var html = HtmlExporter.Render(Rows);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html); // encoded, not raw
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("<strong>1</strong> updates available", html);
        Assert.Contains("<strong>2</strong> programs scanned", html);
        Assert.Contains("prefers-color-scheme: dark", html);            // dark-aware report
    }

    [Fact]
    public void Files_AreWrittenUtf8()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var csvPath = Path.Combine(dir.FullName, "r.csv");
            var htmlPath = Path.Combine(dir.FullName, "r.html");
            CsvExporter.Write(Rows, csvPath);
            HtmlExporter.Write(Rows, htmlPath);
            Assert.Contains("Git.Git", File.ReadAllText(csvPath));
            Assert.Contains("WinUpdateChecker Report", File.ReadAllText(htmlPath));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test --filter ExporterTests`
Expected: FAIL; exporters do not exist.

- [ ] **Step 4: Write the implementations**

`src/WinUpdateChecker.Core/Export/CsvExporter.cs`:

```csharp
using System.Text;
using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Export;

/// <summary>CSV report, same columns as v1 Export-CsvReport.</summary>
public static class CsvExporter
{
    public static string Render(IReadOnlyList<ReportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name,Publisher,Current,Available,Status,PackageId,PackageSource");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',',
                new[] { r.Name, r.Publisher ?? "", r.Current, r.Available, r.Status, r.PackageId, r.PackageSource }
                    .Select(Quote)));
        }
        return sb.ToString();
    }

    public static void Write(IReadOnlyList<ReportRow> rows, string path)
        => File.WriteAllText(path, Render(rows), Encoding.UTF8);

    private static string Quote(string field)
        => field.Contains(',') || field.Contains('"') || field.Contains('\n')
            ? $"\"{field.Replace("\"", "\"\"")}\""
            : field;
}
```

`src/WinUpdateChecker.Core/Export/HtmlExporter.cs` (template ported verbatim from v1; `$$"""` raw string so CSS braces stay literal and interpolation uses `{{ }}`):

```csharp
using System.Net;
using System.Text;
using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Export;

/// <summary>Stand-alone styled HTML report (light/dark aware), ported from v1 Export-HtmlReport.</summary>
public static class HtmlExporter
{
    public static string Render(IReadOnlyList<ReportRow> rows)
    {
        var generated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var availableCount = rows.Count(r => r.IsUpdate);

        var rowsHtml = new StringBuilder();
        foreach (var r in rows)
        {
            var cls = r.IsUpdate ? "update" : "ok";
            rowsHtml.AppendLine(
                $"<tr class='{cls}'><td>{E(r.Name)}</td><td>{E(r.Publisher)}</td><td>{E(r.Current)}</td>" +
                $"<td>{E(r.Available)}</td><td>{E(r.Status)}</td><td>{E(r.PackageId)}</td><td>{E(r.PackageSource)}</td></tr>");
        }

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
<title>{{AppInfo.ProductName}} Report</title>
<style>
  :root { color-scheme: light dark; }
  body { font-family: 'Segoe UI', system-ui, sans-serif; margin: 2rem; max-width: 1300px; }
  h1 { margin: 0 0 0.25rem; }
  .meta { color: #666; margin-bottom: 1.5rem; font-size: 0.9rem; }
  .summary { display: flex; gap: 1rem; margin-bottom: 1.5rem; }
  .card { padding: 0.75rem 1.25rem; border-radius: 6px; background: #f4f4f7; border: 1px solid #ddd; }
  .card.warn { background: #fff7d6; border-color: #e0c870; }
  table { width: 100%; border-collapse: collapse; font-size: 0.9rem; }
  th, td { text-align: left; padding: 0.4rem 0.6rem; border-bottom: 1px solid #e5e5e5; }
  th { background: #f0f0f5; position: sticky; top: 0; }
  tr.update { background: #fffbe6; }
  tr.update td:nth-child(5) { color: #b06a00; font-weight: 600; }
  tbody tr:hover { background: #f7f7fb; }
  footer { margin-top: 2rem; color: #888; font-size: 0.8rem; }
  @media (prefers-color-scheme: dark) {
    body { background: #1a1a1d; color: #e8e8e8; }
    .card { background: #25252b; border-color: #38383f; }
    .card.warn { background: #3a3422; border-color: #6b5d2a; }
    th { background: #25252b; }
    th, td { border-color: #2e2e34; }
    tr.update { background: #2e2820; }
    tr.update td:nth-child(5) { color: #f0c060; }
    tbody tr:hover { background: #28282e; }
  }
</style>
</head>
<body>
<h1>{{AppInfo.ProductName}} Report</h1>
<div class="meta">Generated {{generated}} &middot; v{{AppInfo.Version}}</div>
<div class="summary">
  <div class="card warn"><strong>{{availableCount}}</strong> updates available</div>
  <div class="card"><strong>{{rows.Count}}</strong> programs scanned</div>
</div>
<table>
  <thead>
    <tr><th>Name</th><th>Publisher</th><th>Current</th><th>Available</th><th>Status</th><th>Package Id</th><th>Source</th></tr>
  </thead>
  <tbody>
{{rowsHtml.ToString()}}
  </tbody>
</table>
<footer>
  Generated by <a href="{{AppInfo.RepoUrl}}">{{AppInfo.ProductName}}</a>.
</footer>
</body>
</html>
""";
    }

    public static void Write(IReadOnlyList<ReportRow> rows, string path)
        => File.WriteAllText(path, Render(rows), Encoding.UTF8);

    private static string E(string? text) => WebUtility.HtmlEncode(text ?? "");
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter ExporterTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/WinUpdateChecker.Core/Export src/WinUpdateChecker.Core/AppInfo.cs tests/WinUpdateChecker.Core.Tests/Export
git commit -m "feat(core): port CSV and HTML report exporters"
```

---

### Task 16: CLI options parser

Pure argument parsing in Core (testable); execution lives in the App (Task 17). Implements the spec's flag table and mode rules: `--no-gui` / `--export-csv` / `--export-html` force headless; `--source` / `--include-system-components` are mode-independent.

**Files:**
- Create: `src/WinUpdateChecker.Core/Cli/CliOptions.cs`
- Test: `tests/WinUpdateChecker.Core.Tests/Cli/CliOptionsTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using WinUpdateChecker.Core.Cli;

namespace WinUpdateChecker.Core.Tests.Cli;

public class CliOptionsTests
{
    [Fact]
    public void NoArgs_IsGuiMode()
    {
        var o = CliOptions.Parse([]);
        Assert.False(o.IsHeadless);
        Assert.Null(o.Error);
        Assert.Empty(o.Sources);
        Assert.False(o.IncludeSystemComponents);
    }

    [Fact]
    public void NoGui_IsHeadless()
        => Assert.True(CliOptions.Parse(["--no-gui"]).IsHeadless);

    [Fact]
    public void ExportFlags_ImplyHeadlessAndCapturePaths()
    {
        var o = CliOptions.Parse(["--export-csv", @"C:\tmp\r.csv"]);
        Assert.True(o.IsHeadless);
        Assert.Equal(@"C:\tmp\r.csv", o.ExportCsv);

        var h = CliOptions.Parse(["--export-html", "report.html"]);
        Assert.True(h.IsHeadless);
        Assert.Equal("report.html", h.ExportHtml);
    }

    [Fact]
    public void SourceFlag_ParsesCommaSeparatedList_CaseInsensitive()
    {
        var o = CliOptions.Parse(["--source", "winget,SCOOP"]);
        Assert.Equal(new[] { "winget", "scoop" }, o.Sources);
        Assert.False(o.IsHeadless); // mode-independent, applies to GUI too
    }

    [Fact]
    public void IncludeSystemComponents_IsModeIndependent()
    {
        var o = CliOptions.Parse(["--include-system-components"]);
        Assert.True(o.IncludeSystemComponents);
        Assert.False(o.IsHeadless);
    }

    [Fact]
    public void InvalidSource_IsAnError()
    {
        var o = CliOptions.Parse(["--source", "npm"]);
        Assert.NotNull(o.Error);
        Assert.Contains("npm", o.Error);
        Assert.True(o.IsHeadless); // errors are reported on the console
    }

    [Theory]
    [InlineData("--export-csv")]
    [InlineData("--export-html")]
    [InlineData("--source")]
    public void FlagMissingItsValue_IsAnError(string flag)
        => Assert.NotNull(CliOptions.Parse([flag]).Error);

    [Fact]
    public void UnknownFlag_IsAnError()
        => Assert.Contains("--frobnicate", CliOptions.Parse(["--frobnicate"]).Error);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter CliOptionsTests`
Expected: FAIL; `CliOptions` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
namespace WinUpdateChecker.Core.Cli;

/// <summary>
/// Parsed command-line options. Mode rule (per spec): --no-gui and the export flags run
/// headless; --source and --include-system-components also apply when launching the GUI.
/// </summary>
public sealed record CliOptions(
    bool NoGui,
    string? ExportCsv,
    string? ExportHtml,
    IReadOnlyList<string> Sources,
    bool IncludeSystemComponents,
    string? Error)
{
    private static readonly string[] ValidSources = ["winget", "scoop", "chocolatey"];

    public bool IsHeadless => NoGui || ExportCsv is not null || ExportHtml is not null || Error is not null;

    public static CliOptions Parse(string[] args)
    {
        var noGui = false;
        string? exportCsv = null, exportHtml = null;
        var sources = new List<string>();
        var includeSystem = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--no-gui":
                    noGui = true;
                    break;
                case "--include-system-components":
                    includeSystem = true;
                    break;
                case "--export-csv":
                    if (++i >= args.Length) return Fail("--export-csv requires a file path.");
                    exportCsv = args[i];
                    break;
                case "--export-html":
                    if (++i >= args.Length) return Fail("--export-html requires a file path.");
                    exportHtml = args[i];
                    break;
                case "--source":
                    if (++i >= args.Length) return Fail("--source requires a comma-separated list (winget,scoop,chocolatey).");
                    foreach (var s in args[i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        var lower = s.ToLowerInvariant();
                        if (!ValidSources.Contains(lower))
                            return Fail($"Unknown source '{s}'. Valid sources: winget, scoop, chocolatey.");
                        if (!sources.Contains(lower)) sources.Add(lower);
                    }
                    break;
                default:
                    return Fail($"Unknown argument '{args[i]}'.");
            }
        }

        return new CliOptions(noGui, exportCsv, exportHtml, sources, includeSystem, null);

        static CliOptions Fail(string message) => new(false, null, null, [], false, message);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter CliOptionsTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/WinUpdateChecker.Core/Cli tests/WinUpdateChecker.Core.Tests/Cli
git commit -m "feat(core): add CLI options parser"
```

---

### Task 17: App entry point; CLI runner + placeholder window

One exe, two modes. WPF stays the default; CLI flags attach to the parent console and run headless. The real GUI replaces the placeholder in Plan 2.

**Files:**
- Create: `src/WinUpdateChecker.App/Program.cs`, `src/WinUpdateChecker.App/Cli/CliRunner.cs`
- Modify: `src/WinUpdateChecker.App/WinUpdateChecker.App.csproj`, `src/WinUpdateChecker.App/MainWindow.xaml`

- [ ] **Step 1: Point the project at a custom Main**

In `src/WinUpdateChecker.App/WinUpdateChecker.App.csproj`, add inside the first `<PropertyGroup>`:

```xml
<StartupObject>WinUpdateChecker.App.Program</StartupObject>
<AssemblyName>WinUpdateChecker</AssemblyName>
```

- [ ] **Step 2: Write Program.cs**

```csharp
using System.Runtime.InteropServices;
using WinUpdateChecker.Core.Cli;

namespace WinUpdateChecker.App;

public static class Program
{
    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    [STAThread]
    public static int Main(string[] args)
    {
        var options = CliOptions.Parse(args);
        if (options.IsHeadless)
        {
            // A WinExe has no console; borrow the parent shell's so output is visible
            // when invoked from PowerShell/cmd or a scheduled task.
            AttachConsole(AttachParentProcess);
            return Cli.CliRunner.RunAsync(options).GetAwaiter().GetResult();
        }

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
```

- [ ] **Step 3: Write CliRunner**

`src/WinUpdateChecker.App/Cli/CliRunner.cs`:

```csharp
using WinUpdateChecker.Core;
using WinUpdateChecker.Core.Cli;
using WinUpdateChecker.Core.Export;
using WinUpdateChecker.Core.Scanning;
using WinUpdateChecker.Core.Sources;

namespace WinUpdateChecker.App.Cli;

/// <summary>Headless mode: scan, then print a table or write an export. Exit 0 = success, 1 = failure.</summary>
public static class CliRunner
{
    private const string Usage = """
        Usage: WinUpdateChecker [options]
          (no options)                  Launch the GUI
          --no-gui                      Print available updates to the console
          --export-csv <path>           Write a CSV report and exit
          --export-html <path>          Write a stand-alone HTML report and exit
          --source <list>               Restrict sources: winget,scoop,chocolatey
          --include-system-components   Include Windows components and hotfixes
        """;

    public static async Task<int> RunAsync(CliOptions options)
    {
        if (options.Error is not null)
        {
            Console.Error.WriteLine($"error: {options.Error}");
            Console.Error.WriteLine(Usage);
            return 1;
        }

        try
        {
            var runner = new ProcessRunner();
            var sources = new IPackageSource[] { new WingetSource(runner), new ScoopSource(runner), new ChocoSource(runner) };
            var service = new ScanService(new RegistryScanner(), sources);

            Console.WriteLine("Scanning installed programs and querying package managers...");
            var result = await service.ScanAsync(new ScanOptions(options.Sources, options.IncludeSystemComponents));

            foreach (var warning in result.Warnings)
                Console.Error.WriteLine($"warning: {warning}");
            if (result.EnabledSources.Count == 0)
                Console.Error.WriteLine("warning: no supported package manager found (winget, scoop, chocolatey).");

            if (options.ExportCsv is not null)
            {
                CsvExporter.Write(result.Rows, options.ExportCsv);
                Console.WriteLine($"CSV written to {options.ExportCsv}");
                return 0;
            }
            if (options.ExportHtml is not null)
            {
                HtmlExporter.Write(result.Rows, options.ExportHtml);
                Console.WriteLine($"HTML written to {options.ExportHtml}");
                return 0;
            }

            PrintTable(result);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static void PrintTable(ScanResult result)
    {
        var updates = result.Rows.Where(r => r.IsUpdate).ToList();
        Console.WriteLine();
        Console.WriteLine($"Updates available: {updates.Count} of {result.Rows.Count} entries");
        Console.WriteLine($"Sources queried: {string.Join(", ", result.EnabledSources)}");
        Console.WriteLine();
        if (updates.Count == 0) return;

        string[] headers = ["Name", "Current", "Available", "PackageId", "Source"];
        var cells = updates
            .Select(u => new[] { u.Name, u.Current, u.Available, u.PackageId, u.PackageSource })
            .ToList();
        var widths = headers
            .Select((h, i) => Math.Max(h.Length, cells.Max(row => row[i].Length)))
            .ToArray();

        Console.WriteLine(FormatRow(headers, widths));
        Console.WriteLine(FormatRow(widths.Select(w => new string('-', w)).ToArray(), widths));
        foreach (var row in cells)
            Console.WriteLine(FormatRow(row, widths));

        static string FormatRow(string[] cols, int[] widths)
            => string.Join("  ", cols.Select((c, i) => c.PadRight(widths[i]))).TrimEnd();
    }
}
```

- [ ] **Step 4: Replace the placeholder MainWindow content**

`src/WinUpdateChecker.App/MainWindow.xaml`: replace the template `<Grid>` with:

```xml
<Grid>
    <TextBlock HorizontalAlignment="Center" VerticalAlignment="Center"
               FontSize="16" Foreground="Gray"
               Text="WinUpdateChecker v2; GUI arrives in Plan 2. Use --no-gui for the console." />
</Grid>
```

And set the window title attribute on the root element: `Title="WinUpdateChecker" Width="1100" Height="650"`.

- [ ] **Step 5: Build and smoke-test both modes manually**

```powershell
dotnet build
# Headless scan (real winget run; takes 5-15 s):
dotnet run --project src/WinUpdateChecker.App -- --no-gui
# Export check:
dotnet run --project src/WinUpdateChecker.App -- --export-html "$env:TEMP\wuc-smoke.html"
# Open and eyeball: Invoke-Item "$env:TEMP\wuc-smoke.html"
# Error path returns exit code 1:
dotnet run --project src/WinUpdateChecker.App -- --source npm; echo "exit=$LASTEXITCODE"
```

Expected: `--no-gui` prints the counts line and a column-aligned table matching what `.\UpdateChecker.ps1 -NoGui` reports on the same machine (same update count; this is the parity check); the HTML report opens and looks like the v1 report; the bad-source run prints `error: Unknown source 'npm'...` and `exit=1`.

- [ ] **Step 6: Run the full test suite one last time**

Run: `dotnet test`
Expected: all tests pass.

- [ ] **Step 7: Commit**

```powershell
git add src/WinUpdateChecker.App
git commit -m "feat(app): add CLI mode and app entry point"
```

---

## Done criteria for Plan 1

- `dotnet test` green; Core has no UI references.
- `WinUpdateChecker --no-gui` reports the same updates as `UpdateChecker.ps1 -NoGui` on the same machine.
- `--export-csv` / `--export-html` / `--source` / `--include-system-components` all work headless.
- Plan 2 (WPF GUI: shell, pages, upgrade flow, History, Settings) and Plan 3 (publishing, installer, CI, README, retiring the PS script) follow as separate plan documents.
