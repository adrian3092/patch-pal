using PatchPal.App.ViewModels;
using PatchPal.Core.Models;

namespace PatchPal.App.Tests.ViewModels;

public class UpdateRowViewModelTests
{
    private static UpdateRowViewModel GitRow()
        => new(new ReportRow("Git", "The Git Development Community", "2.44.0", "2.45.2", "Update available", "Git.Git", "winget"));

    [Fact]
    public void FailureDetails_ShowsTheWholeMessageThenTheLog()
    {
        // The row trims a long message to keep the app name visible; hovering shows all of it.
        var row = GitRow();
        row.StateMessage = "Installed by a different installer type — update it from within the app.";
        row.Log = "A newer version was found, but the install technology is different.";

        Assert.Equal(
            "Installed by a different installer type — update it from within the app.\n\n" +
            "A newer version was found, but the install technology is different.",
            row.FailureDetails);
    }

    [Fact]
    public void FailureDetails_WithoutALog_IsJustTheMessage()
    {
        var row = GitRow();
        row.StateMessage = "Elevation was declined (UAC prompt cancelled).";
        Assert.Equal("Elevation was declined (UAC prompt cancelled).", row.FailureDetails);
    }

    [Fact]
    public void FailureDetails_IsRefreshedWhenTheResultArrives()
    {
        var row = GitRow();
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        row.StateMessage = "Fatal installer error (1603).";
        row.Log = "installer log";

        Assert.Equal(2, changed.Count(p => p == nameof(UpdateRowViewModel.FailureDetails)));
    }
}
