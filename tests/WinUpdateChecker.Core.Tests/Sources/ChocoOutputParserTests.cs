using WinUpdateChecker.Core.Sources;

namespace WinUpdateChecker.Core.Tests.Sources;

public class ChocoOutputParserTests
{
    // `choco outdated -r` emits pipe-delimited rows: name|current|available|pinned
    private const string Fixture =
        "7zip|23.1.0|24.8.0|false\n" +
        "git.install|2.44.0|2.45.2|false\n";

    [Fact]
    public void Parse_ExtractsPipeDelimitedRows()
    {
        var result = ChocoOutputParser.Parse(Fixture);
        Assert.Equal(2, result.Count);
        var git = result[1];
        Assert.Equal("git.install", git.Name);
        Assert.Equal("git.install", git.Id);
        Assert.Equal("2.44.0", git.Current);
        Assert.Equal("2.45.2", git.Available);
        Assert.Equal("chocolatey", git.PackageSource);
    }

    [Fact]
    public void Parse_SkipsLinesWithTooFewFields()
    {
        var result = ChocoOutputParser.Parse("garbage line\nok|1.0|2.0|false\n");
        Assert.Single(result);
        Assert.Equal("ok", result[0].Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n\n")]
    public void Parse_ReturnsEmptyOnEmptyOutput(string raw)
        => Assert.Empty(ChocoOutputParser.Parse(raw));
}
