using WinUpdateChecker.Core.History;

namespace WinUpdateChecker.Core.Tests.History;

public class HistoryStoreTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory();
    public void Dispose() => _dir.Delete(recursive: true);

    private HistoryStore Store() => new(_dir.FullName);

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
        File.WriteAllText(Path.Combine(_dir.FullName, "history.json"), "[{ broken");
        var store = Store();
        Assert.Empty(store.Load());
        store.Append(Entry());                                   // store still usable
        Assert.Single(store.Load());
    }
}
