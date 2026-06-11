using System.IO;
using WinUpdateChecker.Core.Cli;
using WinUpdateChecker.Core.Export;
using WinUpdateChecker.Core.Scanning;
using WinUpdateChecker.Core.Sources;

namespace WinUpdateChecker.App.Cli;

/// <summary>Headless mode: scan, then print a table or write an export. Exit 0 = success, 1 = failure.</summary>
public static class CliRunner
{
    private const string Usage = """
        Usage: WinUpdateChecker [options]
          (no options)                  Launch the GUI
          --no-gui                      Print available updates to the console
          --export-csv <path>           Write a CSV report and exit
          --export-html <path>          Write a stand-alone HTML report and exit
          --source <list>               Restrict sources: winget,scoop,chocolatey
          --include-system-components   Include Windows components and hotfixes
        """;

    public static async Task<int> RunAsync(CliOptions options)
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; }
        catch (IOException) { /* no console handle — keep default */ }

        if (options.Error is not null)
        {
            Console.Error.WriteLine($"error: {options.Error}");
            Console.Error.WriteLine(Usage);
            return 1;
        }

        try
        {
            var runner = new ProcessRunner();
            var sources = new IPackageSource[] { new WingetSource(runner), new ScoopSource(runner), new ChocoSource(runner) };
            var service = new ScanService(new RegistryScanner(), sources);

            Console.WriteLine("Scanning installed programs and querying package managers...");
            var result = await service.ScanAsync(new ScanOptions(options.Sources, options.IncludeSystemComponents));

            foreach (var warning in result.Warnings)
                Console.Error.WriteLine($"warning: {warning}");
            if (result.EnabledSources.Count == 0)
                Console.Error.WriteLine("warning: no supported package manager found (winget, scoop, chocolatey).");

            if (options.ExportCsv is not null)
            {
                CsvExporter.Write(result.Rows, options.ExportCsv);
                Console.WriteLine($"CSV written to {options.ExportCsv}");
                return 0;
            }
            if (options.ExportHtml is not null)
            {
                HtmlExporter.Write(result.Rows, options.ExportHtml);
                Console.WriteLine($"HTML written to {options.ExportHtml}");
                return 0;
            }

            PrintTable(result);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static void PrintTable(ScanResult result)
    {
        var updates = result.Rows.Where(r => r.IsUpdate).ToList();
        Console.WriteLine();
        Console.WriteLine($"Updates available: {updates.Count} of {result.Rows.Count} entries");
        Console.WriteLine($"Sources queried: {string.Join(", ", result.EnabledSources)}");
        Console.WriteLine();
        if (updates.Count == 0) return;

        string[] headers = ["Name", "Current", "Available", "PackageId", "Source"];
        var cells = updates
            .Select(u => new[] { u.Name, u.Current, u.Available, u.PackageId, u.PackageSource })
            .ToList();
        var widths = headers
            .Select((h, i) => Math.Max(h.Length, cells.Max(row => row[i].Length)))
            .ToArray();

        Console.WriteLine(FormatRow(headers, widths));
        Console.WriteLine(FormatRow(widths.Select(w => new string('-', w)).ToArray(), widths));
        foreach (var row in cells)
            Console.WriteLine(FormatRow(row, widths));

        static string FormatRow(string[] cols, int[] widths)
            => string.Join("  ", cols.Select((c, i) => c.PadRight(widths[i]))).TrimEnd();
    }
}
