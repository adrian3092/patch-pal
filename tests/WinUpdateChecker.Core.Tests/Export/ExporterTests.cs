using WinUpdateChecker.Core.Export;
using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Tests.Export;

public class ExporterTests
{
    private static readonly IReadOnlyList<ReportRow> Rows =
    [
        new("Git", "The Git \"Team\", Inc.", "2.44.0", "2.45.2", "Update available", "Git.Git", "winget"),
        new("<script>alert(1)</script>", "Evil, Corp", "1.0", "", "Up to date / unknown", "", ""),
    ];

    [Fact]
    public void Csv_WritesHeaderAndQuotedFields()
    {
        var csv = CsvExporter.Render(Rows);
        var lines = csv.TrimEnd().Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        Assert.Equal("Name,Publisher,Current,Available,Status,PackageId,PackageSource", lines[0]);
        Assert.Equal(3, lines.Length);
        // Comma-containing and quote-containing fields are quoted with doubled quotes:
        Assert.Contains("\"The Git \"\"Team\"\", Inc.\"", lines[1]);
        Assert.Contains("\"Evil, Corp\"", lines[2]);
    }

    [Fact]
    public void Html_EncodesContentAndCountsUpdates()
    {
        var html = HtmlExporter.Render(Rows);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html); // encoded, not raw
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("<strong>1</strong> updates available", html);
        Assert.Contains("<strong>2</strong> programs scanned", html);
        Assert.Contains("prefers-color-scheme: dark", html);            // dark-aware report
    }

    [Fact]
    public void Files_AreWrittenUtf8()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var csvPath = Path.Combine(dir.FullName, "r.csv");
            var htmlPath = Path.Combine(dir.FullName, "r.html");
            CsvExporter.Write(Rows, csvPath);
            HtmlExporter.Write(Rows, htmlPath);
            Assert.Contains("Git.Git", File.ReadAllText(csvPath));
            Assert.Contains("WinUpdateChecker Report", File.ReadAllText(htmlPath));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
