using WinUpdateChecker.App.ViewModels;
using WinUpdateChecker.Core.Models;
using WinUpdateChecker.Core.Scanning;
using ScanState = WinUpdateChecker.App.ScanState; // 'App.ScanState' would resolve to the App class

namespace WinUpdateChecker.App.Tests.ViewModels;

public class AllAppsViewModelTests
{
    private static ScanResult Result() => new(
        [
            new ReportRow("7zip", "Igor Pavlov", "23.01", "24.08", "Update available", "7zip.7zip", "winget"),
            new ReportRow("Notepad++", "Don Ho", "8.6", "", "Up to date / unknown", "", ""),
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
        Assert.True(vm.MatchesFilter(notepad, "unknown"));       // status text
        Assert.False(vm.MatchesFilter(notepad, "winget"));
    }
}
