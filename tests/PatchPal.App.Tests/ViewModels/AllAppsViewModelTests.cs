using PatchPal.App.ViewModels;
using PatchPal.Core.Models;
using PatchPal.Core.Scanning;
using ScanState = PatchPal.App.ScanState; // 'App.ScanState' would resolve to the App class

namespace PatchPal.App.Tests.ViewModels;

public class AllAppsViewModelTests
{
    private static ScanResult Result() => new(
        [
            new ReportRow("7zip", "Igor Pavlov", "23.01", "24.08", "Update available", "7zip.7zip", "winget"),
            new ReportRow("Notepad++", "Don Ho", "8.6", "", "Not tracked", "", ""),
        ],
        ["winget"], 2, []);

    [Fact]
    public void Refresh_ShowsAllRows_NotJustUpdates()
    {
        var state = new ScanState { LastResult = Result() };
        var vm = new AllAppsViewModel(state);
        vm.Refresh();
        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal("2 programs · 1 update", vm.SummaryText);
    }

    [Fact]
    public void Refresh_EmptyWhenNoScanYet()
    {
        var vm = new AllAppsViewModel(new ScanState());
        vm.Refresh();
        Assert.Empty(vm.Rows);
        Assert.Equal("No scan yet — run a scan from the Updates page", vm.SummaryText);
    }

    [Fact]
    public void MatchesFilter_ChecksNamePublisherAndStatus()
    {
        var vm = new AllAppsViewModel(new ScanState { LastResult = Result() });
        vm.Refresh();
        var notepad = vm.Rows.First(r => r.Name == "Notepad++");
        Assert.True(vm.MatchesFilter(notepad, "don ho"));        // publisher, case-insensitive
        Assert.True(vm.MatchesFilter(notepad, "tracked"));       // status text
        Assert.False(vm.MatchesFilter(notepad, "winget"));
    }

    [Fact]
    public void SelfUpdatingApp_ShowsUpdatesItself_LikeTheUpdatesPage()
    {
        var result = new ScanResult(
            [
                new ReportRow("Microsoft Edge", "Microsoft Corporation", "154.0.4258.37", "154.0.4258.48", "Update available", "Microsoft.Edge", "winget"),
                new ReportRow("7zip", "Igor Pavlov", "23.01", "24.08", "Update available", "7zip.7zip", "winget"),
            ],
            ["winget"], 2, []);
        var vm = new AllAppsViewModel(new ScanState { LastResult = result });
        vm.Refresh();
        var edge = vm.Rows.Single(r => r.Name == "Microsoft Edge");

        Assert.Equal("Updates itself", edge.DisplayStatus);
        Assert.Equal("Update available", vm.Rows.Single(r => r.Name == "7zip").DisplayStatus);
        Assert.True(vm.MatchesFilter(edge, "itself"));           // the filter matches what's shown
    }
}
