using PatchPal.Core.Cli;
using PatchPal.Core.Settings;

namespace PatchPal.Core.Tests.Cli;

public class CliOptionsTests
{
    [Fact]
    public void NoArgs_IsGuiMode()
    {
        var o = CliOptions.Parse([]);
        Assert.False(o.IsHeadless);
        Assert.Null(o.Error);
        Assert.Empty(o.Sources);
        Assert.False(o.IncludeSystemComponents);
    }

    [Fact]
    public void NoGui_IsHeadless()
        => Assert.True(CliOptions.Parse(["--no-gui"]).IsHeadless);

    [Fact]
    public void ExportFlags_ImplyHeadlessAndCapturePaths()
    {
        var o = CliOptions.Parse(["--export-csv", @"C:\tmp\r.csv"]);
        Assert.True(o.IsHeadless);
        Assert.Equal(@"C:\tmp\r.csv", o.ExportCsv);

        var h = CliOptions.Parse(["--export-html", "report.html"]);
        Assert.True(h.IsHeadless);
        Assert.Equal("report.html", h.ExportHtml);
    }

    [Fact]
    public void SourceFlag_ParsesCommaSeparatedList_CaseInsensitive()
    {
        var o = CliOptions.Parse(["--source", "winget,SCOOP"]);
        Assert.Equal(new[] { "winget", "scoop" }, o.Sources);
        Assert.False(o.IsHeadless); // mode-independent, applies to GUI too
    }

    [Fact]
    public void IncludeSystemComponents_IsModeIndependent()
    {
        var o = CliOptions.Parse(["--include-system-components"]);
        Assert.True(o.IncludeSystemComponents);
        Assert.False(o.IsHeadless);
    }

    [Fact]
    public void InvalidSource_IsAnError()
    {
        var o = CliOptions.Parse(["--source", "npm"]);
        Assert.NotNull(o.Error);
        Assert.Contains("npm", o.Error);
        Assert.True(o.IsHeadless); // errors are reported on the console
    }

    [Theory]
    [InlineData("--export-csv")]
    [InlineData("--export-html")]
    [InlineData("--source")]
    public void FlagMissingItsValue_IsAnError(string flag)
        => Assert.NotNull(CliOptions.Parse([flag]).Error);

    [Fact]
    public void UnknownFlag_IsAnError()
        => Assert.Contains("--frobnicate", CliOptions.Parse(["--frobnicate"]).Error);

    // Headless runs (scheduled reports) scan what the Settings page describes unless a flag says otherwise.
    [Fact]
    public void ToScanOptions_WithoutSourceFlag_UsesTheSettingsSources()
    {
        var options = CliOptions.Parse(["--no-gui"]).ToScanOptions(new AppSettings { DisabledSources = ["scoop"] });
        Assert.Equal(["winget", "chocolatey"], options.Sources);
    }

    [Fact]
    public void ToScanOptions_SourceFlag_WinsOverSettings()
    {
        var options = CliOptions.Parse(["--no-gui", "--source", "scoop"]).ToScanOptions(new AppSettings { DisabledSources = ["scoop"] });
        Assert.Equal(["scoop"], options.Sources);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]    // turned on in Settings
    [InlineData(true, false, true)]    // turned on by the flag
    public void ToScanOptions_IncludesSystemComponents_WhenFlagOrSettingsSaySo(bool flag, bool setting, bool expected)
    {
        string[] args = flag ? ["--no-gui", "--include-system-components"] : ["--no-gui"];
        var options = CliOptions.Parse(args).ToScanOptions(new AppSettings { IncludeSystemComponents = setting });
        Assert.Equal(expected, options.IncludeSystemComponents);
    }
}
