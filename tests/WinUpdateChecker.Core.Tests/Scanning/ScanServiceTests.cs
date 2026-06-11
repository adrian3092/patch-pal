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
