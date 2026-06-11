using WinUpdateChecker.Core.Sources;

namespace WinUpdateChecker.Core.Tests.Sources;

public class WingetOutputParserTests
{
    // The parser slices by header column positions, so the fixture is built with PadRight
    // to guarantee data cells start exactly under their header words.
    private static string Row(string name, string id, string version, string available, string source)
        => name.PadRight(52) + id.PadRight(33) + version.PadRight(15) + available.PadRight(15) + source;

    private static readonly string Fixture = string.Join('\n',
        Row("Name", "Id", "Version", "Available", "Source"),
        new string('-', 120),
        Row("Microsoft Visual C++ 2015-2022 Redistributable (…", "Microsoft.VCRedist.2015+.x64", "14.44.35211.0", "14.51.36231.0", "winget"),
        Row("Git", "Git.Git", "2.44.0", "2.45.2", "winget"),
        "2 upgrades available.",
        "");

    [Fact]
    public void Parse_ExtractsAllUpgradeRows()
    {
        var result = WingetOutputParser.Parse(Fixture);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Parse_ExtractsColumnsByPosition()
    {
        var git = WingetOutputParser.Parse(Fixture)[1];
        Assert.Equal("Git", git.Name);
        Assert.Equal("Git.Git", git.Id);
        Assert.Equal("2.44.0", git.Current);
        Assert.Equal("2.45.2", git.Available);
        Assert.Equal("winget", git.PackageSource);
    }

    [Fact]
    public void Parse_PreservesEllipsisTruncatedNames()
    {
        var vc = WingetOutputParser.Parse(Fixture)[0];
        Assert.Equal("Microsoft Visual C++ 2015-2022 Redistributable (…", vc.Name);
        Assert.Equal("Microsoft.VCRedist.2015+.x64", vc.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n  \n")]
    [InlineData("No installed package found matching input criteria.\n")] // no header line
    public void Parse_ReturnsEmptyOnNonTableOutput(string raw)
        => Assert.Empty(WingetOutputParser.Parse(raw));

    [Fact]
    public void Parse_SkipsMalformedShortLines()
    {
        var raw = Fixture.Replace("2 upgrades available.\n", "x\n2 upgrades available.\n");
        Assert.Equal(2, WingetOutputParser.Parse(raw).Count);
    }
}
