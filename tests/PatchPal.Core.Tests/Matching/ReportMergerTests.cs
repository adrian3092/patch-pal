using PatchPal.Core.Matching;
using PatchPal.Core.Models;

namespace PatchPal.Core.Tests.Matching;

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
    public void ExactNameMatch_ComparesInThePackageManagersVersionScheme()
    {
        // The .NET SDK registers 8.4.2226.27003 in Programs and Features, which winget maps to
        // package version 8.0.422. Comparing the registry number with 8.0.425 hid the update.
        var rows = ReportMerger.Merge(
            [new InstalledProgram("Microsoft .NET SDK 8.0.422 (x64)", "8.4.2226.27003", "Microsoft Corporation")],
            [new UpgradeCandidate("Microsoft .NET SDK 8.0.422 (x64)", "Microsoft.DotNet.SDK.8", "8.0.422", "8.0.425", "winget")],
            ["winget"]);
        var sdk = Assert.Single(rows);
        Assert.True(sdk.IsUpdate);
        Assert.Equal("8.0.422", sdk.Current);                // not "8.4.2226.27003 -> 8.0.425"
        Assert.Equal("8.0.425", sdk.Available);
    }

    [Fact]
    public void ExactNameMatch_WithUnknownInstalledVersion_FallsBackToRegistryVersion()
    {
        // `winget upgrade --include-unknown` prints "Unknown" when it can't read the version.
        var rows = ReportMerger.Merge(
            [new InstalledProgram("Contoso App", "2.0.0", "Contoso")],
            [new UpgradeCandidate("Contoso App", "Contoso.App", "Unknown", "2.0.0", "winget")],
            ["winget"]);
        Assert.False(Assert.Single(rows).IsUpdate);
    }

    [Fact]
    public void ExactNameMatch_FromChocolatey_KeepsTheRegistryVersion()
    {
        // Chocolatey names are package ids and its installed version is its own record, which
        // goes stale when the app updates itself; only winget reports the install's version.
        var rows = ReportMerger.Merge(
            [new InstalledProgram("Discord", "1.0.9040", "Discord Inc.")],
            [new UpgradeCandidate("discord", "discord", "1.0.9015", "1.0.9040", "chocolatey")],
            ["chocolatey"]);
        Assert.False(Assert.Single(rows).IsUpdate);
    }

    [Fact]
    public void FuzzyMatch_AlreadyAtAvailableVersion_IsNotAnUpdate()
    {
        // winget keyed this row off an older entry (14.44) than the redistributable actually
        // installed, which is already at the available version.
        var rows = ReportMerger.Merge(
            [new InstalledProgram("Microsoft Visual C++ 2015-2022 Redistributable (x64) - 14.51.36231", "14.51.36231.0", "Microsoft Corporation")],
            [new UpgradeCandidate("Microsoft Visual C++ 2015-2022 Redistributable (…", "Microsoft.VCRedist.2015+.x64", "14.44.35211.0", "14.51.36231.0", "winget")],
            ["winget"]);
        Assert.DoesNotContain(rows, r => r.IsUpdate);
    }

    [Fact]
    public void UnrelatedProgram_IsNotFalselyMatched()
    {
        var chrome = MergedRows().First(r => r.Name == "Google Chrome");
        Assert.False(chrome.IsUpdate);
        Assert.Equal("Not tracked", chrome.Status);
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

    [Fact]
    public void EmptyProgramName_DoesNotFuzzyMatchAnything()
    {
        // v1 guarded fuzzy passes against empty names; "" would otherwise build a \b\b regex
        // that matches any upgrade and steals its source-only row.
        var rows = ReportMerger.Merge(
            [new InstalledProgram("", "1.0", null)],
            [new UpgradeCandidate("some-cli-tool", "some-cli-tool", "1.0", "2.0", "scoop")],
            ["scoop"]);
        Assert.Contains(rows, r => r.Name == "some-cli-tool" && r.Status == "Update available (scoop only)");
        Assert.Contains(rows, r => r.Name == "" && !r.IsUpdate);
    }
}
