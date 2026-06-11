using System.Text;
using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Export;

/// <summary>CSV report, same columns as v1 Export-CsvReport.</summary>
public static class CsvExporter
{
    public static string Render(IReadOnlyList<ReportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name,Publisher,Current,Available,Status,PackageId,PackageSource");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',',
                new[] { r.Name, r.Publisher ?? "", r.Current, r.Available, r.Status, r.PackageId, r.PackageSource }
                    .Select(Quote)));
        }
        return sb.ToString();
    }

    public static void Write(IReadOnlyList<ReportRow> rows, string path)
        => File.WriteAllText(path, Render(rows), Encoding.UTF8);

    private static string Quote(string field)
        => field.Contains(',') || field.Contains('"') || field.Contains('\n')
            ? $"\"{field.Replace("\"", "\"\"")}\""
            : field;
}
