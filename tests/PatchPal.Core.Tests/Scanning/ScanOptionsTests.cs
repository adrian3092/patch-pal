using PatchPal.Core.Scanning;
using PatchPal.Core.Settings;

namespace PatchPal.Core.Tests.Scanning;

public class ScanOptionsTests
{
    [Fact]
    public void FromSettings_LeavesOutTheSourcesSwitchedOff()
    {
        var options = ScanOptions.FromSettings(new AppSettings { DisabledSources = ["scoop"] });
        Assert.Equal(["winget", "chocolatey"], options.Sources);
    }

    [Fact]
    public void FromSettings_CarriesTheSystemComponentsChoice()
        => Assert.True(ScanOptions.FromSettings(new AppSettings { IncludeSystemComponents = true }).IncludeSystemComponents);
}
