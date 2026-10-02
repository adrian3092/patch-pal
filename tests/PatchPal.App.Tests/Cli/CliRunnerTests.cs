using System.IO;
using PatchPal.App.Cli;
using PatchPal.Core.Cli;
using PatchPal.Core.Scanning;
using PatchPal.Core.Settings;
using PatchPal.Core.Sources;

namespace PatchPal.App.Tests.Cli;

public class CliRunnerTests
{
    [Fact]
    public async Task Headless_ScansOnlyTheSourcesEnabledInSettings()
    {
        // A scheduled report must respect the Settings page, like the Updates page does.
        var winget = new FakeRunner();
        var choco = new FakeRunner { ListOutput = "7zip|23.01|24.08|false\n" };
        var service = new ScanService(new FakePrograms(), [new WingetSource(winget), new ChocoSource(choco)]);
        var output = new StringWriter();

        var code = await CliRunner.RunAsync(CliOptions.Parse(["--no-gui"]), service,
            () => new AppSettings { DisabledSources = ["chocolatey"] }, output, new StringWriter());

        Assert.Equal(0, code);
        Assert.Contains("Sources queried: winget", output.ToString());
        Assert.Empty(choco.Calls);                               // switched off, never run
    }

    [Fact]
    public async Task Headless_SettingsItCannotRead_WarnsAndUsesDefaults()
    {
        var service = new ScanService(new FakePrograms(), [new WingetSource(new FakeRunner())]);
        var error = new StringWriter();

        var code = await CliRunner.RunAsync(CliOptions.Parse(["--no-gui"]), service,
            () => throw new UnauthorizedAccessException("Access to the path is denied."), new StringWriter(), error);

        Assert.Equal(0, code);                                   // the report still runs
        Assert.Contains("couldn't read settings", error.ToString());
    }
}
