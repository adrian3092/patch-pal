using System.IO;
using PatchPal.App.ViewModels;
using PatchPal.Core.History;

namespace PatchPal.App.Tests.ViewModels;

public class HistoryViewModelTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory();
    public void Dispose() => _dir.Delete(recursive: true);

    private HistoryStore Store() => new(_dir.FullName);

    private static HistoryEntry Entry(DateTimeOffset when, bool success = true) =>
        new(when, "Git", "Git.Git", "winget", "2.44", "2.45", success, success ? "Succeeded." : "boom", "log text");

    [Fact]
    public void Refresh_GroupsByDate_NewestFirst()
    {
        var store = Store();
        store.Append(Entry(DateTimeOffset.Now.AddDays(-1)));
        store.Append(Entry(DateTimeOffset.Now));
        var vm = new HistoryViewModel(store);
        vm.Refresh();
        Assert.Equal(2, vm.Groups.Count);
        Assert.True(vm.Groups[0].Date > vm.Groups[1].Date);      // newest group first
        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public void Refresh_EmptyStore_SetsIsEmpty()
    {
        var vm = new HistoryViewModel(Store());
        vm.Refresh();
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public void Refresh_HistoryItCannotRead_SaysSoInsteadOfLookingEmpty()
    {
        var store = Store();
        store.Append(Entry(DateTimeOffset.Now));
        var vm = new HistoryViewModel(store);

        // Another program has the file open for writing, so it can't be read right now.
        using (new FileStream(Path.Combine(_dir.FullName, "history.json"), FileMode.Open, FileAccess.Read, FileShare.Write))
            vm.Refresh();

        Assert.True(vm.IsEmpty);
        Assert.StartsWith("Couldn't read the update history", vm.EmptyText);
    }

    [Fact]
    public void Refresh_EmptyStore_SaysNothingWasApplied()
    {
        var vm = new HistoryViewModel(Store());
        vm.Refresh();
        Assert.Equal("No updates applied yet", vm.EmptyText);
    }

    [Fact]
    public async Task Clear_EmptiesStoreAndGroups()
    {
        var store = Store();
        store.Append(Entry(DateTimeOffset.Now));
        var vm = new HistoryViewModel(store) { ConfirmInteraction = _ => Task.FromResult(true) };
        vm.Refresh();
        await vm.ClearCommand.ExecuteAsync(null);
        Assert.True(vm.IsEmpty);
        Assert.Empty(store.Load());
    }

    [Fact]
    public async Task Clear_Declined_KeepsEntries()
    {
        var store = Store();
        store.Append(Entry(DateTimeOffset.Now));
        var vm = new HistoryViewModel(store) { ConfirmInteraction = _ => Task.FromResult(false) };
        vm.Refresh();
        await vm.ClearCommand.ExecuteAsync(null);
        Assert.Single(store.Load());
    }

    [Theory]
    [InlineData("en-GB", "Wednesday, 5 March 2025")]
    [InlineData("en-US", "Wednesday, March 5, 2025")]
    public void GroupTitle_OlderDates_UseTheRegionFormat(string culture, string expected)
    {
        using var _ = new CultureScope(culture);

        var group = new HistoryGroup(new DateOnly(2025, 3, 5), []);

        Assert.Equal(expected, group.Title);
    }
}
