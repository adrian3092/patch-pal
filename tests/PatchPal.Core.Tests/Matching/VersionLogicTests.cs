using PatchPal.Core.Matching;

namespace PatchPal.Core.Tests.Matching;

public class VersionLogicTests
{
    // Ports tests/Merge.Tests.ps1 "Version comparison"
    [Theory]
    [InlineData("14.44.35211.0", "14.51.36231.0", true)]   // older -> update
    [InlineData("14.51.36231.0", "14.51.36231.0", false)]  // equal -> no update
    [InlineData("14.52.0.0", "14.51.36231.0", false)]      // installed newer -> no update
    [InlineData("< 8.0.425", "8.0.425", true)]             // winget: installed is below 8.0.425 -> update
    public void IsNewerVersion_ComparesParsableVersions(string current, string available, bool expected)
        => Assert.Equal(expected, VersionLogic.IsNewerVersion(current, available));

    [Theory]
    [InlineData("8.6", "8.6.0", false)]                    // the same version, written with fewer parts
    [InlineData("5.17", "5.17.0.0", false)]
    [InlineData("8.6.0", "8.6", false)]
    [InlineData("8.6", "8.6.1", true)]
    public void IsNewerVersion_TreatsMissingPartsAsZero(string current, string available, bool expected)
        => Assert.Equal(expected, VersionLogic.IsNewerVersion(current, available));

    [Theory]
    [InlineData("121", "121", false)]                      // whole numbers, with no dot to anchor on
    [InlineData("121", "122", true)]
    [InlineData("122", "121", false)]
    [InlineData("2024", "2024.05.28", true)]
    public void IsNewerVersion_ComparesWholeNumberVersions(string current, string available, bool expected)
        => Assert.Equal(expected, VersionLogic.IsNewerVersion(current, available));

    [Theory]
    [InlineData("", "1.2.3")]          // unparseable current -> assume update
    [InlineData("abc", "1.2.3")]
    [InlineData("1.2.3", "unknown")]   // unparseable available -> assume update
    public void IsNewerVersion_AssumesUpdateWhenUnparseable(string current, string available)
        => Assert.True(VersionLogic.IsNewerVersion(current, available));

    [Theory]
    [InlineData("8.0.422", true)]
    [InlineData("> 8.0.422", true)]     // winget marks out-of-range installs with < or >
    [InlineData("Unknown", false)]      // `winget upgrade --include-unknown`
    [InlineData("", false)]
    [InlineData("121", true)]           // a whole number is a version too
    public void IsParsable_DetectsAUsableVersion(string text, bool expected)
        => Assert.Equal(expected, VersionLogic.IsParsable(text));

    [Fact]
    public void GetVersionValue_StripsNonVersionCharacters()
        => Assert.Equal(new Version(14, 44, 35211, 0), VersionLogic.GetVersionValue("v14.44.35211.0 (x64)"));

    [Fact]
    public void GetVersionValue_ReturnsZeroWhenUnparseable()
        => Assert.Equal(new Version(0, 0), VersionLogic.GetVersionValue("not a version"));
}
