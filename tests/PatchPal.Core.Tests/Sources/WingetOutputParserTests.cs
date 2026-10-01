using PatchPal.Core.Sources;

namespace PatchPal.Core.Tests.Sources;

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

    // Regression: winget uses bare \r (not \r\n) for progress spinner lines when stdout
    // is redirected.  The parser must still find the header and parse data rows correctly.
    [Fact]
    public void Parse_HandlesBareCrSeparators()
    {
        // Simulate real redirected winget output: progress lines end with bare \r,
        // then the table arrives with \r\n.  Build the fixture with bare \r between lines.
        var bareCrFixture = string.Join('\r',
            Row("Name", "Id", "Version", "Available", "Source"),
            new string('-', 120),
            Row("Git", "Git.Git", "2.44.0", "2.45.2", "winget"),
            "1 upgrades available.",
            "");

        var result = WingetOutputParser.Parse(bareCrFixture);
        Assert.Single(result);
        Assert.Equal("Git", result[0].Name);
        Assert.Equal("Git.Git", result[0].Id);
        Assert.Equal("2.44.0", result[0].Current);
        Assert.Equal("2.45.2", result[0].Available);
    }

    // Regression: \r\n-terminated input (normal Windows stdout) must also parse correctly.
    // Splitting on both '\r' and '\n' leaves an empty string between the \r and \n of each
    // pair; the parser skips blank lines when it looks for the header and the rows.
    [Fact]
    public void Parse_HandlesCrLfSeparators()
    {
        var crlfFixture = string.Join("\r\n",
            Row("Name", "Id", "Version", "Available", "Source"),
            new string('-', 120),
            Row("Git", "Git.Git", "2.44.0", "2.45.2", "winget"),
            "1 upgrades available.",
            "");

        var result = WingetOutputParser.Parse(crlfFixture);
        Assert.Single(result);
        Assert.Equal("Git", result[0].Name);
        Assert.Equal("Git.Git", result[0].Id);
    }

    // winget prints its headers and footer in the Windows display language. Labels and footers
    // below are winget's own translations (Localization/Resources/<lang>/winget.resw).
    [Theory]
    [InlineData("Name", "ID", "Version", "Verfügbar", "Quelle", "2 Aktualisierungen verfügbar.")] // de-DE
    [InlineData("Nom", "ID", "Version", "Disponible", "Source", "2 mises à niveau disponibles.")] // fr-FR
    [InlineData("Имя", "ИД", "Версия", "Доступно", "Источник", "Доступны обновления: 2.")]       // ru-RU
    public void Parse_ReadsTranslatedOutput(string name, string id, string version, string available, string source, string footer)
    {
        var raw = string.Join('\n',
            Row(name, id, version, available, source),
            new string('-', 120),
            Row("Microsoft Visual C++ 2015-2022 Redistributable (…", "Microsoft.VCRedist.2015+.x64", "14.44.35211.0", "14.51.36231.0", "winget"),
            Row("Git", "Git.Git", "2.44.0", "2.45.2", "winget"),
            footer,
            "");

        var result = WingetOutputParser.Parse(raw);

        Assert.Equal(2, result.Count);                     // the footer is not read as a row
        Assert.Equal("Git", result[1].Name);
        Assert.Equal("Git.Git", result[1].Id);
        Assert.Equal("2.44.0", result[1].Current);
        Assert.Equal("2.45.2", result[1].Available);
    }

    // A column: the text, its width in console cells (Chinese, Japanese, and Korean characters
    // take two), then padding to the column width plus winget's one-space separator.
    private static string Cell(string text, int textCells, int columnCells)
        => text + new string(' ', columnCells - textCells + 1);

    [Fact]
    public void Parse_ReadsKoreanOutput_WithSpacedHeadersAndWideCharacters()
    {
        // ko-KR labels: 이름 (4 cells), 장치 ID (7), 버전 (4), 사용 가능 (9), 원본 — two contain spaces.
        var raw = string.Join('\n',
            Cell("이름", 4, 8) + Cell("장치 ID", 7, 15) + Cell("버전", 4, 6) + Cell("사용 가능", 9, 9) + "원본",
            new string('-', 48),
            Cell("카카오톡", 8, 8) + Cell("Kakao.KakaoTalk", 15, 15) + Cell("3.4.0", 5, 6) + Cell("3.5.0", 5, 9) + "winget",
            Cell("Git", 3, 8) + Cell("Git.Git", 7, 15) + Cell("2.44.0", 6, 6) + Cell("2.45.2", 6, 9) + "winget",
            "2 업그레이드를 사용할 수 있습니다.",
            "");

        var result = WingetOutputParser.Parse(raw);

        Assert.Equal(2, result.Count);
        Assert.Equal("카카오톡", result[0].Name);
        Assert.Equal("Kakao.KakaoTalk", result[0].Id);
        Assert.Equal("3.4.0", result[0].Current);
        Assert.Equal("3.5.0", result[0].Available);
        Assert.Equal("Git.Git", result[1].Id);
    }

    [Fact]
    public void Parse_AlignsWideCharacterNames_OnEnglishOutput()
    {
        // "微信" is 2 characters but 4 cells wide; slicing by character index misreads the row.
        var raw = string.Join('\n',
            Cell("Name", 4, 4) + Cell("Id", 2, 14) + Cell("Version", 7, 7) + Cell("Available", 9, 9) + "Source",
            new string('-', 44),
            Cell("微信", 4, 4) + Cell("Tencent.WeChat", 14, 14) + Cell("3.9.10", 6, 7) + Cell("3.9.12", 6, 9) + "winget",
            "1 upgrades available.",
            "");

        var wechat = Assert.Single(WingetOutputParser.Parse(raw));

        Assert.Equal("微信", wechat.Name);
        Assert.Equal("Tencent.WeChat", wechat.Id);
        Assert.Equal("3.9.10", wechat.Current);
        Assert.Equal("3.9.12", wechat.Available);
    }

    // Names in other scripts, with their widths in console cells (Unicode East Asian Width):
    // wide and fullwidth characters take two cells; narrow symbols and accents take one.
    [Theory]
    [InlineData("ゲーム", 6)]          // katakana
    [InlineData("ＡＢＣ Tools", 12)]   // fullwidth letters
    [InlineData("🚀 Launcher", 11)]    // wide emoji
    [InlineData("⭐ Star", 7)]          // wide symbol
    [InlineData("🖥 Desktop", 9)]       // narrow pictograph
    [InlineData("𠀀 App", 6)]          // CJK Extension B, a surrogate pair
    [InlineData("Café", 4)]       // "e" plus a combining accent is one character
    public void Parse_AlignsNamesInAnyScript(string name, int nameCells)
    {
        var raw = string.Join('\n',
            Cell("Name", 4, nameCells) + Cell("Id", 2, 8) + Cell("Version", 7, 7) + Cell("Available", 9, 9) + "Source",
            new string('-', nameCells + 1 + 8 + 1 + 7 + 1 + 9 + 1 + 6),
            Cell(name, nameCells, nameCells) + Cell("Some.App", 8, 8) + Cell("1.0", 3, 7) + Cell("2.0", 3, 9) + "winget",
            "1 upgrades available.",
            "");

        var app = Assert.Single(WingetOutputParser.Parse(raw));

        Assert.Equal(name, app.Name);
        Assert.Equal("Some.App", app.Id);
        Assert.Equal("2.0", app.Available);
    }

    [Fact]
    public void Parse_KeepsReadingPastARowItCannotAlign()
    {
        // One row that doesn't line up (e.g. a character measured differently than winget
        // measures it) must not hide the updates listed after it.
        var raw = string.Join('\n',
            Row("Name", "Id", "Version", "Available", "Source"),
            new string('-', 120),
            Row("Alpha", "Alpha.App", "1.0", "1.1", "winget"),
            " " + Row("Misaligned", "Misaligned.App", "1.0", "2.0", "winget"),
            Row("Git", "Git.Git", "2.44.0", "2.45.2", "winget"),
            "3 upgrades available.",
            "");

        Assert.Equal(["Alpha.App", "Git.Git"], WingetOutputParser.Parse(raw).Select(u => u.Id));
    }

    [Fact]
    public void Parse_FirstRowNameStartingWithASpace_KeepsTheColumns()
    {
        // Rows are sorted by name, so a display name that starts with a space comes first.
        var raw = string.Join('\n',
            Row("Name", "Id", "Version", "Available", "Source"),
            new string('-', 120),
            Row(" Contoso Tools", "Contoso.Tools", "1.0", "1.1", "winget"),
            Row("Git", "Git.Git", "2.44.0", "2.45.2", "winget"),
            "2 upgrades available.",
            "");

        var result = WingetOutputParser.Parse(raw);

        Assert.Equal(2, result.Count);
        Assert.Equal("Contoso Tools", result[0].Name);
        Assert.Equal("Contoso.Tools", result[0].Id);
        Assert.Equal("1.1", result[0].Available);
    }

    [Fact]
    public void Parse_IgnoresTheExplicitTargetingTableAfterTheFooter()
    {
        // Upgrades that require explicit targeting get their own table after the footer.
        // `winget upgrade --all` skips them, and so does Patch Pal.
        var raw = string.Join('\n',
            Row("Name", "Id", "Version", "Available", "Source"),
            new string('-', 120),
            Row("Git", "Git.Git", "2.44.0", "2.45.2", "winget"),
            "1 upgrades available.",
            "",
            "The following packages have an upgrade available, but require explicit targeting for upgrade:",
            Row("Name", "Id", "Version", "Available", "Source"),
            new string('-', 120),
            Row("Explicit App", "Explicit.App", "1.0", "2.0", "winget"),
            "");

        Assert.Equal(["Git.Git"], WingetOutputParser.Parse(raw).Select(u => u.Id));
    }

    [Fact]
    public void Parse_ReadsATableWithoutTheSourceColumn()
    {
        // winget drops a column whose values are all blank.
        var raw = string.Join('\n',
            Row("Name", "Id", "Version", "Available", "").TrimEnd(),
            new string('-', 120),
            Row("Git", "Git.Git", "2.44.0", "2.45.2", "").TrimEnd(),
            "1 upgrades available.",
            "");

        var git = Assert.Single(WingetOutputParser.Parse(raw));

        Assert.Equal("Git.Git", git.Id);
        Assert.Equal("2.45.2", git.Available);
    }

    [Fact]
    public void Parse_ShortDashRunIsNotTheTableRule()
    {
        var raw = string.Join('\n',
            "Note:",
            "---",
            Row("Name", "Id", "Version", "Available", "Source"),
            new string('-', 120),
            Row("Git", "Git.Git", "2.44.0", "2.45.2", "winget"),
            "1 upgrades available.",
            "");

        Assert.Equal("Git.Git", Assert.Single(WingetOutputParser.Parse(raw)).Id);
    }

    [Fact]
    public void Parse_ReadsRealWingetOutput()
    {
        // Captured from winget 1.29 (`winget search`, which uses the same table printer as
        // `winget upgrade`): real padding, a separator one dash longer than the rows, CRLF.
        var raw = string.Join("\r\n",
            "Name                            Id                           Version                 Source",
            "--------------------------------------------------------------------------------------------",
            "Microsoft .NET SDK 10.0         Microsoft.DotNet.SDK.10      10.0.401                winget",
            "Microsoft .NET SDK 9.0          Microsoft.DotNet.SDK.9       9.0.318                 winget",
            "Microsoft .NET SDK 11.0 Preview Microsoft.DotNet.SDK.Preview 11.0.100-rc.1.26425.128 winget",
            "");

        var result = WingetOutputParser.Parse(raw);

        Assert.Equal(["Microsoft.DotNet.SDK.10", "Microsoft.DotNet.SDK.9", "Microsoft.DotNet.SDK.Preview"], result.Select(u => u.Id));
        Assert.Equal("Microsoft .NET SDK 11.0 Preview", result[2].Name);
        Assert.Equal("11.0.100-rc.1.26425.128", result[2].Current);
    }
}
