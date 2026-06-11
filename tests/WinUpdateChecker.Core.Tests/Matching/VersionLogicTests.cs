using WinUpdateChecker.Core.Matching;

namespace WinUpdateChecker.Core.Tests.Matching;

public class VersionLogicTests
{
    // Ports tests/Merge.Tests.ps1 "Version comparison"
    [Theory]
    [InlineData("14.44.35211.0", "14.51.36231.0", true)]   // older -> update
    [InlineData("14.51.36231.0", "14.51.36231.0", false)]  // equal -> no update
    [InlineData("14.52.0.0", "14.51.36231.0", false)]      // installed newer -> no update
    public void IsNewerVersion_ComparesParsableVersions(string current, string available, bool expected)
        => Assert.Equal(expected, VersionLogic.IsNewerVersion(current, available));

    [Theory]
    [InlineData("", "1.2.3")]          // unparseable current -> assume update
    [InlineData("abc", "1.2.3")]
    [InlineData("1.2.3", "unknown")]   // unparseable available -> assume update
    public void IsNewerVersion_AssumesUpdateWhenUnparseable(string current, string available)
        => Assert.True(VersionLogic.IsNewerVersion(current, available));

    [Fact]
    public void GetVersionValue_StripsNonVersionCharacters()
        => Assert.Equal(new Version(14, 44, 35211, 0), VersionLogic.GetVersionValue("v14.44.35211.0 (x64)"));

    [Fact]
    public void GetVersionValue_ReturnsZeroWhenUnparseable()
        => Assert.Equal(new Version(0, 0), VersionLogic.GetVersionValue("not a version"));
}
