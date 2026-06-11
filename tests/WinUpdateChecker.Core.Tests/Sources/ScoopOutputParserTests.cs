using WinUpdateChecker.Core.Sources;

namespace WinUpdateChecker.Core.Tests.Sources;

public class ScoopOutputParserTests
{
    private const string Fixture =
        "Name    Installed Version  Latest Version     Missing Dependencies  Info\n" +
        "----    -----------------  --------------     --------------------  ----\n" +
        "7zip    23.01              24.08\n" +
        "git     2.44.0             2.45.2             \n" +
        "neovim  0.9.5              0.9.5                                   \n";  // current == latest -> not an upgrade

    [Fact]
    public void Parse_ExtractsOnlyRowsWithNewerVersions()
    {
        var result = ScoopOutputParser.Parse(Fixture);
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, u => u.Name == "neovim");
    }

    [Fact]
    public void Parse_UsesNameAsId_AndTagsScoopSource()
    {
        var git = ScoopOutputParser.Parse(Fixture).First(u => u.Name == "git");
        Assert.Equal("git", git.Id);
        Assert.Equal("2.44.0", git.Current);
        Assert.Equal("2.45.2", git.Available);
        Assert.Equal("scoop", git.PackageSource);
    }

    [Fact]
    public void Parse_StripsTrailingInfoFromLatestVersion()
    {
        // "Latest Version" cell can carry trailing columns; only the first token counts.
        var raw =
            "Name    Installed Version  Latest Version     Missing Dependencies  Info\n" +
            "----    -----------------  --------------     --------------------  ----\n" +
            "vlc     3.0.20             3.0.21             Held package\n";
        var vlc = ScoopOutputParser.Parse(raw).Single();
        Assert.Equal("3.0.21", vlc.Available);
    }

    [Theory]
    [InlineData("")]
    [InlineData("WARN  Scoop update check skipped\n")]
    public void Parse_ReturnsEmptyOnNonTableOutput(string raw)
        => Assert.Empty(ScoopOutputParser.Parse(raw));
}
