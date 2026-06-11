using WinUpdateChecker.Core.History;
using WinUpdateChecker.Core.Sources;
using WinUpdateChecker.Core.Tests.Sources;
using WinUpdateChecker.Core.Upgrading;

namespace WinUpdateChecker.Core.Tests.Upgrading;

public class BatchUpgradeRunnerTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory();
    public void Dispose() => _dir.Delete(recursive: true);

    private (BatchUpgradeRunner Batch, FakeProcessRunner Fake, HistoryStore History) Setup()
    {
        var fake = new FakeProcessRunner();
        var runner = new UpgradeRunner([new WingetSource(fake), new ScoopSource(fake), new ChocoSource(fake)]);
        var history = new HistoryStore(_dir.FullName);
        return (new BatchUpgradeRunner(runner, history), fake, history);
    }

    private static BatchItem Item(string source, string id) => new(source, id, id, "1.0", "2.0");

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
