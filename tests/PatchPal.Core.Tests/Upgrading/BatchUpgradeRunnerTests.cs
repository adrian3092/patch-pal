using PatchPal.Core.History;
using PatchPal.Core.Sources;
using PatchPal.Core.Tests.Sources;
using PatchPal.Core.Upgrading;

namespace PatchPal.Core.Tests.Upgrading;

public class BatchUpgradeRunnerTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory();
    public void Dispose() => _dir.Delete(recursive: true);

    private (BatchUpgradeRunner Batch, FakeProcessRunner Fake, HistoryStore History) Setup()
    {
        var fake = new FakeProcessRunner();
        var runner = new UpgradeRunner([new WingetSource(fake), new ScoopSource(fake), new ChocoSource(fake, FakeChocolateyLocator.Installed)]);
        var history = new HistoryStore(_dir.FullName);
        return (new BatchUpgradeRunner(runner, history), fake, history);
    }

    private static BatchItem Item(string source, string id) => new(source, id, id, "1.0", "2.0");

    // History that can never be saved: its folder would sit under a file, so it can't be created.
    private (BatchUpgradeRunner Batch, FakeProcessRunner Fake) SetupWithUnsavableHistory()
    {
        var notAFolder = Path.Combine(_dir.FullName, "not-a-folder");
        File.WriteAllText(notAFolder, "");
        var fake = new FakeProcessRunner();
        var runner = new UpgradeRunner([new WingetSource(fake), new ScoopSource(fake), new ChocoSource(fake, FakeChocolateyLocator.Installed)]);
        return (new BatchUpgradeRunner(runner, new HistoryStore(Path.Combine(notAFolder, "PatchPal"))), fake);
    }

    [Fact]
    public async Task HistoryThatCannotBeSaved_DoesNotStopTheBatch()
    {
        // Each upgrade has already run when its entry is written, so a failed write must not
        // skip the rest of the batch.
        var (batch, fake) = SetupWithUnsavableHistory();

        var results = await batch.RunAsync([Item("winget", "A.A"), Item("winget", "B.B")]);

        Assert.Equal(["A.A", "B.B"], results.Select(r => r.Id));
        Assert.Equal(2, fake.Calls.Count);
        Assert.All(results, r => Assert.True(r.Success));
    }

    [Fact]
    public async Task HistoryThatCannotBeSaved_IsReportedWithTheResult()
    {
        var (batch, _) = SetupWithUnsavableHistory();
        UpgradeResult? finished = null;

        await batch.RunAsync([Item("winget", "A.A")], onItemFinished: (_, result) => finished = result);

        Assert.NotNull(finished);
        Assert.NotNull(finished.HistoryError);
    }

    [Fact]
    public async Task SavedHistory_HasNoHistoryError()
    {
        var (batch, _, _) = Setup();
        var results = await batch.RunAsync([Item("winget", "A.A")]);
        Assert.Null(Assert.Single(results).HistoryError);
    }

    [Fact]
    public async Task RunsAllItems_AndRecordsHistory()
    {
        var (batch, _, history) = Setup();
        var results = await batch.RunAsync([Item("winget", "Git.Git"), Item("scoop", "neovim")]);
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
        Assert.Equal(2, history.Load().Count);
    }

    [Fact]
    public async Task Failure_IsRecorded_AndDoesNotBlockOthers()
    {
        var (batch, fake, history) = Setup();
        fake.ExitCode = 1603;                                    // every upgrade "fails"
        var results = await batch.RunAsync([Item("winget", "A.A"), Item("winget", "B.B")]);
        Assert.Equal(2, results.Count);                          // second still ran
        Assert.All(results, r => Assert.False(r.Success));
        Assert.All(history.Load(), e => Assert.False(e.Success));
    }

    [Fact]
    public async Task Callbacks_FireStartAndFinishPerItem()
    {
        var (batch, _, _) = Setup();
        var started = new List<string>();
        var finished = new List<string>();
        await batch.RunAsync([Item("winget", "Git.Git")],
            onItemStarted: i => started.Add(i.PackageId),
            onItemFinished: (i, r) => finished.Add($"{i.PackageId}:{r.Success}"));
        Assert.Equal(new[] { "Git.Git" }, started);
        Assert.Equal(new[] { "Git.Git:True" }, finished);
    }

    [Fact]
    public async Task SameSourceItems_RunSequentiallyInOrder()
    {
        var (batch, fake, _) = Setup();
        await batch.RunAsync([Item("winget", "A.A"), Item("winget", "B.B"), Item("winget", "C.C")]);
        var wingetArgs = fake.Calls.Select(c => c.Arguments).ToList();
        Assert.Equal(3, wingetArgs.Count);
        Assert.Contains("A.A", wingetArgs[0]);                   // strict submission order preserved
        Assert.Contains("B.B", wingetArgs[1]);
        Assert.Contains("C.C", wingetArgs[2]);
    }
}
