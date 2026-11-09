using PatchPal.Core.History;

namespace PatchPal.Core.Tests.History;

public class HistoryStoreTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory();
    public void Dispose() => _dir.Delete(recursive: true);

    private HistoryStore Store() => new(_dir.FullName);

    private string HistoryPath => Path.Combine(_dir.FullName, "history.json");

    private string[] Backups() => Directory.GetFiles(_dir.FullName, "history.json*.bak");

    // How another program can hold the file: writers may open it, readers may not.
    private FileStream BlockReads() => new(HistoryPath, FileMode.Open, FileAccess.Read, FileShare.Write);

    private static HistoryEntry Entry(string name = "7-Zip", bool success = true) =>
        new(DateTimeOffset.UtcNow, name, "7zip.7zip", "winget", "23.01", "24.08", success,
            success ? "Succeeded." : "winget exited with code 1603.", success ? "" : "captured log");

    [Fact]
    public void Load_EmptyWhenNoFile()
        => Assert.Empty(Store().Load());

    [Fact]
    public void Append_PersistsAcrossInstances()
    {
        Store().Append(Entry("Git"));
        Store().Append(Entry("7-Zip", success: false));
        var all = Store().Load();
        Assert.Equal(2, all.Count);
        Assert.Equal("Git", all[0].Name);
        Assert.False(all[1].Success);
        Assert.Equal("captured log", all[1].Log);
    }

    [Fact]
    public void Clear_EmptiesTheLog()
    {
        var store = Store();
        store.Append(Entry());
        store.Clear();
        Assert.Empty(store.Load());
    }

    [Fact]
    public void Load_CorruptFile_BacksUpAndStartsFresh()
    {
        File.WriteAllText(HistoryPath, "[{ broken");
        var store = Store();
        Assert.Empty(store.Load());
        Assert.Equal("[{ broken", File.ReadAllText(Assert.Single(Backups())));
        store.Append(Entry());                                   // store still usable
        Assert.Single(store.Load());
    }

    [Fact]
    public void Load_CorruptFileTwice_KeepsBothBackups()
    {
        // A second corruption used to overwrite the backup of the first.
        File.WriteAllText(HistoryPath, "[{ first");
        Store().Load();
        File.WriteAllText(HistoryPath, "[{ second");
        Store().Load();

        Assert.Equal(["[{ first", "[{ second"], Backups().Select(File.ReadAllText).Order());
    }

    [Fact]
    public void Append_WhenTheFileCannotBeRead_FailsWithoutTouchingIt()
    {
        // A file it couldn't read used to count as an empty history, so the append saved
        // only the new entry over everything recorded before it.
        var store = Store();
        store.Append(Entry("Git"));
        store.Append(Entry("7-Zip"));

        using (BlockReads())
            Assert.ThrowsAny<IOException>(() => store.Append(Entry("VLC")));

        Assert.Equal(["Git", "7-Zip"], store.Load().Select(e => e.Name));
    }

    [Fact]
    public async Task Append_WhileAnotherProgramBrieflyHasTheFile_WaitsAndSaves()
    {
        // Antivirus and backup tools open a freshly written file for a moment. An append that
        // lands then must wait for them, not fail and lose the entry.
        var store = Store();
        store.Append(Entry("Git"));
        var holder = new FileStream(HistoryPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var released = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            holder.Dispose();
        });

        try
        {
            store.Append(Entry("7-Zip"));
        }
        finally
        {
            await released;
        }

        Assert.Equal(["Git", "7-Zip"], store.Load().Select(e => e.Name));
    }

    [Fact]
    public async Task Append_FromTwoStoresAtOnce_KeepsEveryEntry()
    {
        // Two Patch Pal windows each have their own store; neither may drop the other's entries.
        var first = Store();
        var second = Store();

        await Task.WhenAll(
            Task.Run(() => { for (var i = 0; i < 25; i++) first.Append(Entry($"first {i}")); }),
            Task.Run(() => { for (var i = 0; i < 25; i++) second.Append(Entry($"second {i}")); }));

        Assert.Equal(50, Store().Load().Count);
    }
}
