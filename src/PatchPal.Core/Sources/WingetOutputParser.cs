using PatchPal.Core.Models;

namespace PatchPal.Core.Sources;

/// <summary>
/// Parses the table `winget upgrade` prints. winget translates its headers and footer into the
/// Windows display language, so the parser never matches words: it finds the dashed line under
/// the header, takes each column to start where the header and the first row both start a word,
/// and reads the rows that keep that alignment. Positions are console cells, as winget pads
/// them, so Chinese, Japanese, and Korean text lines up.
/// </summary>
public static class WingetOutputParser
{
    // winget's only all-dash line is the table rule, which spans the table; a short run of
    // dashes elsewhere isn't one.
    private const int MinSeparatorLength = 10;

    public static IReadOnlyList<UpgradeCandidate> Parse(string? raw)
    {
        var lines = SplitLines(raw);
        var separator = Array.FindIndex(lines, IsSeparator);
        if (separator < 0) return [];

        var header = lines.Take(separator).LastOrDefault(l => !string.IsNullOrWhiteSpace(l));
        var rows = TableBody(lines, separator).Select(ConsoleCells.Layout).ToList();
        if (header is null || rows.Count == 0) return [];

        // Name, Id, Version, Available, and Source (winget drops a column when it is all blank).
        var columns = WordStarts(ConsoleCells.Layout(header)).Intersect(WordStarts(rows[0])).Order().ToList();
        if (columns.Count is not (4 or 5)) return [];

        var upgrades = new List<UpgradeCandidate>();
        foreach (var row in rows)
        {
            // The footer and notes don't line up, and neither does a row with a character measured
            // differently than winget measured it. Skip them so one odd row can't hide the rest.
            var starts = WordStarts(row);
            if (!columns.All(starts.Contains)) continue;
            upgrades.Add(new UpgradeCandidate(
                Cell(row, columns, 0), Cell(row, columns, 1), Cell(row, columns, 2), Cell(row, columns, 3), "winget"));
        }
        return upgrades;
    }

    /// <summary>True when the output contains a table, whether or not <see cref="Parse"/> could read it.</summary>
    internal static bool ContainsTable(string? raw) => SplitLines(raw).Any(IsSeparator);

    // Winget uses bare \r (carriage return) as a line separator when stdout is redirected
    // (progress-spinner animation), so split on any CR/LF combination.
    private static string[] SplitLines(string? raw)
        => string.IsNullOrEmpty(raw) ? [] : raw.Split(['\r', '\n']);

    private static bool IsSeparator(string line)
    {
        var trimmed = line.TrimEnd();
        return trimmed.Length >= MinSeparatorLength && trimmed.All(c => c == '-');
    }

    // The non-blank lines after the dashed line, up to the next table: winget lists upgrades that
    // require explicit targeting in a second table after the footer, and `winget upgrade --all`
    // skips them. The line just above that table's dashed line is its header, so it is dropped too.
    private static List<string> TableBody(string[] lines, int separator)
    {
        var next = Array.FindIndex(lines, separator + 1, IsSeparator);
        var body = lines[(separator + 1)..(next < 0 ? lines.Length : next)]
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
        return next < 0 ? body : body.SkipLast(1).ToList();
    }

    // Cells where a word starts: a non-space character after a space. Cell 0 always counts, since
    // the Name column starts there even when a name begins with a space.
    private static HashSet<int> WordStarts(List<(string Text, int Column)> cells)
    {
        var starts = new HashSet<int> { 0 };
        for (var i = 1; i < cells.Count; i++)
            if (cells[i].Text != " " && cells[i - 1].Text == " ")
                starts.Add(cells[i].Column);
        return starts;
    }

    private static string Cell(List<(string Text, int Column)> row, List<int> columns, int index)
    {
        var start = columns[index];
        var end = index + 1 < columns.Count ? columns[index + 1] : int.MaxValue;
        return string.Concat(row.Where(c => c.Column >= start && c.Column < end).Select(c => c.Text)).Trim();
    }
}
