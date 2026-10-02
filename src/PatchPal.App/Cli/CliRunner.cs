using System.IO;
using PatchPal.Core.Cli;
using PatchPal.Core.Export;
using PatchPal.Core.Scanning;
using PatchPal.Core.Settings;
using PatchPal.Core.Sources;

namespace PatchPal.App.Cli;

/// <summary>Headless mode: scan, then print a table or write an export. Exit 0 = success, 1 = failure.</summary>
public static class CliRunner
{
    private const string Usage = """
        Usage: PatchPal [options]
          (no options)                  Launch the GUI
          --no-gui                      Print available updates to the console
          --export-csv <path>           Write a CSV report and exit
          --export-html <path>          Write a stand-alone HTML report and exit
          --source <list>               Restrict sources: winget,scoop,chocolatey
                                        (default: the sources enabled in Settings)
          --include-system-components   Include Windows components and hotfixes
                                        (also on when enabled in Settings)
        """;

    public static Task<int> RunAsync(CliOptions options)
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; }
        catch (IOException) { /* no console handle — keep default */ }

        var runner = new ProcessRunner();
        var sources = new IPackageSource[] { new WingetSource(runner), new ScoopSource(runner), new ChocoSource(runner) };
        return RunAsync(options, new ScanService(new RegistryScanner(), sources),
            () => new SettingsStore().Load(), Console.Out, Console.Error);
    }

    /// <summary>Runs headless with the given scanner and settings, writing to the given streams.</summary>
    public static async Task<int> RunAsync(
        CliOptions options, ScanService service, Func<AppSettings> loadSettings, TextWriter output, TextWriter error)
    {
        if (options.Error is not null)
        {
            error.WriteLine($"error: {options.Error}");
            error.WriteLine(Usage);
            return 1;
        }

        try
        {
            output.WriteLine("Scanning installed programs and querying package managers...");
            var result = await service.ScanAsync(options.ToScanOptions(LoadSettings(loadSettings, error)));

            foreach (var warning in result.Warnings)
                error.WriteLine($"warning: {warning}");
            if (result.EnabledSources.Count == 0)
                error.WriteLine("warning: no supported package manager found (winget, scoop, chocolatey).");

            if (options.ExportCsv is not null)
            {
                CsvExporter.Write(result.Rows, options.ExportCsv);
                output.WriteLine($"CSV written to {options.ExportCsv}");
                return 0;
            }
            if (options.ExportHtml is not null)
            {
                HtmlExporter.Write(result.Rows, options.ExportHtml);
                output.WriteLine($"HTML written to {options.ExportHtml}");
                return 0;
            }

            PrintTable(result, output);
            return 0;
        }
        catch (Exception ex)
        {
            error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    // A scheduled report should still run when settings.json can't be read (locked, or denied
    // to the account running the task); say so and use the defaults.
    private static AppSettings LoadSettings(Func<AppSettings> loadSettings, TextWriter error)
    {
        try
        {
            return loadSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"warning: couldn't read settings, using defaults ({ex.Message})");
            return new AppSettings();
        }
    }

    private static void PrintTable(ScanResult result, TextWriter output)
    {
        var updates = result.Rows.Where(r => r.IsUpdate).ToList();
        output.WriteLine();
        output.WriteLine($"Updates available: {updates.Count} of {result.Rows.Count} entries");
        output.WriteLine($"Sources queried: {string.Join(", ", result.EnabledSources)}");
        output.WriteLine();
        if (updates.Count == 0) return;

        string[] headers = ["Name", "Current", "Available", "PackageId", "Source"];
        var cells = updates
            .Select(u => new[] { u.Name, u.Current, u.Available, u.PackageId, u.PackageSource })
            .ToList();
        var widths = headers
            .Select((h, i) => Math.Max(h.Length, cells.Max(row => row[i].Length)))
            .ToArray();

        output.WriteLine(FormatRow(headers, widths));
        output.WriteLine(FormatRow(widths.Select(w => new string('-', w)).ToArray(), widths));
        foreach (var row in cells)
            output.WriteLine(FormatRow(row, widths));

        static string FormatRow(string[] cols, int[] widths)
            => string.Join("  ", cols.Select((c, i) => c.PadRight(widths[i]))).TrimEnd();
    }
}
