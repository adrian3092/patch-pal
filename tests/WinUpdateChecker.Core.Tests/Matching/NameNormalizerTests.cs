using WinUpdateChecker.Core.Matching;

namespace WinUpdateChecker.Core.Tests.Matching;

public class NameNormalizerTests
{
    // Ports tests/Merge.Tests.ps1 "Name normalization"
    [Fact]
    public void GetMatchBase_PreservesEditionToken()
        => Assert.Equal("microsoft visual c++ 2013 redistributable (x64)",
            NameNormalizer.GetMatchBase("Microsoft Visual C++ 2013 Redistributable (x64) - 12.0.40664"));

    [Fact]
    public void GetMatchBase_StripsWingetEllipsisTruncation()
        => Assert.Equal("microsoft visual c++ 2015-2022 redistributable",
            NameNormalizer.GetMatchBase("Microsoft Visual C++ 2015-2022 Redistributable (…"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetMatchBase_EmptyInputYieldsEmpty(string? name)
        => Assert.Equal("", NameNormalizer.GetMatchBase(name));

    [Fact]
    public void IsBasePrefixMatch_RejectsEdgeVsEdgeWebView2()
        => Assert.False(NameNormalizer.IsBasePrefixMatch("microsoft edge", "microsoft edgewebview2 runtime"));

    [Fact]
    public void IsBasePrefixMatch_AcceptsEqualBases()
        => Assert.True(NameNormalizer.IsBasePrefixMatch("git", "git"));

    [Fact]
    public void IsBasePrefixMatch_AcceptsWordBoundaryPrefix()
        => Assert.True(NameNormalizer.IsBasePrefixMatch(
            "microsoft visual c++ 2015-2022 redistributable",
            "microsoft visual c++ 2015-2022 redistributable (x64)"));

    [Fact]
    public void IsBasePrefixMatch_RejectsShortPrefixes()
        => Assert.False(NameNormalizer.IsBasePrefixMatch("git", "git (64-bit)")); // shorter side < 6 chars
}
