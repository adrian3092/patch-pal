using WinUpdateChecker.Core.Upgrading;

namespace WinUpdateChecker.Core.Tests.Upgrading;

public class PackageIdValidatorTests
{
    // Ports tests/Merge.Tests.ps1 "Package id validation"
    [Theory]
    [InlineData("Microsoft.VCRedist.2015+.x64")]       // winget id with + and .
    [InlineData("Microsoft.DotNet.DesktopRuntime.8")]
    [InlineData("git.install")]                        // choco-style
    [InlineData("extras-app_name")]                    // scoop-style with _ and -
    public void IsSafe_AcceptsLegitimateIds(string id)
        => Assert.True(PackageIdValidator.IsSafe(id));

    [Theory]
    [InlineData("evil; calc.exe")]
    [InlineData("foo & shutdown")]
    [InlineData("a$(rm -rf)")]
    [InlineData("a`nb")]
    [InlineData("pkg with space")]
    [InlineData("")]
    [InlineData(null)]
    public void IsSafe_RejectsInjectionAttempts(string? id)
        => Assert.False(PackageIdValidator.IsSafe(id));
}
