namespace WinUpdateChecker.Core.Cli;

/// <summary>
/// Parsed command-line options. Mode rule (per spec): --no-gui and the export flags run
/// headless; --source and --include-system-components also apply when launching the GUI.
/// </summary>
public sealed record CliOptions(
    bool NoGui,
    string? ExportCsv,
    string? ExportHtml,
    IReadOnlyList<string> Sources,
    bool IncludeSystemComponents,
    string? Error)
{
    private static readonly string[] ValidSources = ["winget", "scoop", "chocolatey"];

    public bool IsHeadless => NoGui || ExportCsv is not null || ExportHtml is not null || Error is not null;

    public static CliOptions Parse(string[] args)
    {
        var noGui = false;
        string? exportCsv = null, exportHtml = null;
        var sources = new List<string>();
        var includeSystem = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--no-gui":
                    noGui = true;
                    break;
                case "--include-system-components":
                    includeSystem = true;
                    break;
                case "--export-csv":
                    if (++i >= args.Length) return Fail("--export-csv requires a file path.");
                    exportCsv = args[i];
                    break;
                case "--export-html":
                    if (++i >= args.Length) return Fail("--export-html requires a file path.");
                    exportHtml = args[i];
                    break;
                case "--source":
                    if (++i >= args.Length) return Fail("--source requires a comma-separated list (winget,scoop,chocolatey).");
                    foreach (var s in args[i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        var lower = s.ToLowerInvariant();
                        if (!ValidSources.Contains(lower))
                            return Fail($"Unknown source '{s}'. Valid sources: winget, scoop, chocolatey.");
                        if (!sources.Contains(lower)) sources.Add(lower);
                    }
                    break;
                default:
                    return Fail($"Unknown argument '{args[i]}'.");
            }
        }

        return new CliOptions(noGui, exportCsv, exportHtml, sources, includeSystem, null);

        static CliOptions Fail(string message) => new(false, null, null, [], false, message);
    }
}
