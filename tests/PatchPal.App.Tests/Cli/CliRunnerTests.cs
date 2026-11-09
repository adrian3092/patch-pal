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
    public async Task Headless_EverySourceSwitchedOff_AsksNoneAndSaysSo()
    {
        var winget = new FakeRunner();
        var service = new ScanService(new FakePrograms(), [new WingetSource(winget)]);
        var error = new StringWriter();

        var code = await CliRunner.RunAsync(CliOptions.Parse(["--no-gui"]), service,
            () => new AppSettings { DisabledSources = ["winget", "scoop", "chocolatey"] }, new StringWriter(), error);

        Assert.Equal(0, code);
        Assert.Empty(winget.Calls);
        Assert.Contains("switched off", error.ToString());
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

    [Fact]
    public async Task Headless_LockedSettingsFile_WarnsAndUsesDefaults()
    {
        // A locked settings.json used to read as defaults without a word.
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var store = new SettingsStore(dir.FullName);
            store.Save(new AppSettings());
            var service = new ScanService(new FakePrograms(), [new WingetSource(new FakeRunner())]);
            var error = new StringWriter();

            int code;
            using (new FileStream(Path.Combine(dir.FullName, "settings.json"), FileMode.Open, FileAccess.Read, FileShare.Write))
                code = await CliRunner.RunAsync(CliOptions.Parse(["--no-gui"]), service, store.Load, new StringWriter(), error);

            Assert.Equal(0, code);
            Assert.Contains("couldn't read settings", error.ToString());
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
