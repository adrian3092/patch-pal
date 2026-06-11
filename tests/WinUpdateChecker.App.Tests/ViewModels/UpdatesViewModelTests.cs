using System.IO;
using WinUpdateChecker.App.ViewModels;
using WinUpdateChecker.Core.History;
using WinUpdateChecker.Core.Models;
using WinUpdateChecker.Core.Scanning;
using WinUpdateChecker.Core.Settings;
using WinUpdateChecker.Core.Sources;
using WinUpdateChecker.Core.Upgrading;
using ScanState = WinUpdateChecker.App.ScanState; // 'App.ScanState' would resolve to the App class

namespace WinUpdateChecker.App.Tests.ViewModels;

public class UpdatesViewModelTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory();
    public void Dispose() => _dir.Delete(recursive: true);

    private (UpdatesViewModel Vm, FakeRunner Runner) Setup()
    {
        // choco reports one outdated package that matches one installed program.
        var runner = new FakeRunner { ListOutput = "7zip|23.01|24.08|false\n" };
        var sources = new IPackageSource[] { new ChocoSource(runner) };
        var scan = new ScanService(
            new FakePrograms(new InstalledProgram("7zip", "23.01", "Igor Pavlov"),
                             new InstalledProgram("Notepad++", "8.6", "Don Ho")),
            sources);
        var history = new HistoryStore(_dir.FullName);
        var batch = new BatchUpgradeRunner(new UpgradeRunner(sources), history);
        var vm = new UpdatesViewModel(scan, batch, new ScanState(), new AppSettings());
        vm.ConfirmInteraction = _ => Task.FromResult(true);      // auto-confirm in tests
        return (vm, runner);
    }

    [Fact]
    public async Task Scan_PopulatesUpdateRowsOnly()
    {
        var (vm, _) = Setup();
        await vm.ScanAsync();
        var row = Assert.Single(vm.Rows);                        // Updates page shows updates only
        Assert.Equal("7zip", row.Name);
        Assert.Equal("23.01", row.Current);
        Assert.Equal("24.08", row.Available);
        Assert.Equal("chocolatey", row.Source);
        Assert.Equal(1, vm.UpdateCount);
        Assert.False(vm.IsScanning);
    }

    [Fact]
    public async Task Filter_MatchesNameIdAndSource()
    {
        var (vm, _) = Setup();
        await vm.ScanAsync();
        var row = vm.Rows[0];
        Assert.True(vm.MatchesFilter(row, "7zi"));
        Assert.True(vm.MatchesFilter(row, "CHOCO"));             // case-insensitive, source too
        Assert.False(vm.MatchesFilter(row, "git"));
        Assert.True(vm.MatchesFilter(row, ""));                  // empty filter matches all
    }

    [Fact]
    public async Task UpdateAll_RunsBatch_AndMarksRowsSucceeded()
    {
        var (vm, _) = Setup();
        await vm.ScanAsync();
        await vm.UpdateAllAsync();
        Assert.Equal(RowState.Succeeded, vm.Rows[0].State);
        Assert.False(vm.IsUpdating);
    }

    [Fact]
    public async Task UpdateAll_FailedUpgrade_MarksRowFailedWithMessage()
    {
        var (vm, runner) = Setup();
        await vm.ScanAsync();
        runner.ExitCode = 1603;
        await vm.UpdateAllAsync();
        Assert.Equal(RowState.Failed, vm.Rows[0].State);
        Assert.Contains("1603", vm.Rows[0].StateMessage);
    }

    [Fact]
    public async Task UpdateSelected_DoesNothingWhenNothingSelected()
    {
        var (vm, runner) = Setup();
        await vm.ScanAsync();
        var callsBefore = runner.Calls.Count;
        await vm.UpdateSelectedAsync();
        Assert.Equal(callsBefore, runner.Calls.Count);           // no upgrade launched
    }

    [Fact]
    public async Task UpdateSelected_RunsOnlyTickedRows()
    {
        var (vm, runner) = Setup();
        await vm.ScanAsync();
        vm.Rows[0].IsSelected = true;
        await vm.UpdateSelectedAsync();
        Assert.Contains(runner.Calls, c => c.Arguments.Contains("7zip") && c.Elevated);
        Assert.Equal(RowState.Succeeded, vm.Rows[0].State);
    }

    [Fact]
    public async Task DeclinedConfirmation_AbortsBatch()
    {
        var (vm, runner) = Setup();
        await vm.ScanAsync();
        vm.ConfirmInteraction = _ => Task.FromResult(false);
        var scanCalls = runner.Calls.Count;
        await vm.UpdateAllAsync();
        Assert.Equal(scanCalls, runner.Calls.Count);             // nothing launched
        Assert.Equal(RowState.Idle, vm.Rows[0].State);
    }

    [Fact]
    public async Task Warnings_SurfaceFromScanResult()
    {
        var throwing = new ThrowingSource();
        var scan = new ScanService(new FakePrograms(), [throwing]);
        var vm = new UpdatesViewModel(scan,
            new BatchUpgradeRunner(new UpgradeRunner([throwing]), new HistoryStore(_dir.FullName)),
            new ScanState(), new AppSettings());
        await vm.ScanAsync();
        Assert.Single(vm.Warnings);
        Assert.Contains("results may be incomplete", vm.Warnings[0]);
    }

    private sealed class ThrowingSource : IPackageSource
    {
        public string Name => "winget";
        public string ExecutableName => "winget";
        public bool IsInstalled() => true;
        public Task<IReadOnlyList<UpgradeCandidate>> ListOutdatedAsync(CancellationToken ct = default)
            => Task.FromException<IReadOnlyList<UpgradeCandidate>>(new InvalidOperationException("boom"));
        public Task<ProcessResult> RunUpgradeAsync(string packageId, CancellationToken ct = default)
            => Task.FromResult(new ProcessResult(0, "", ""));
    }
}
