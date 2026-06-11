using WinUpdateChecker.Core.Matching;
using WinUpdateChecker.Core.Models;
using WinUpdateChecker.Core.Sources;

namespace WinUpdateChecker.Core.Scanning;

public sealed record ScanOptions(IReadOnlyList<string> Sources, bool IncludeSystemComponents)
{
    /// <summary>All detected sources, no system components.</summary>
    public static ScanOptions Default { get; } = new([], false);
}

public sealed record ScanResult(
    IReadOnlyList<ReportRow> Rows,
    IReadOnlyList<string> EnabledSources,
    int ProgramCount,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Runs a full scan: registry read and all enabled source queries in parallel
/// (v1 ran them sequentially), then merges. A source that fails to query becomes
/// a warning — never a scan failure.
/// </summary>
public sealed class ScanService(IInstalledProgramProvider programProvider, IEnumerable<IPackageSource> sources)
{
    private readonly IReadOnlyList<IPackageSource> _sources = sources.ToList();

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        var enabled = _sources
            .Where(s => options.Sources.Count == 0
                || options.Sources.Contains(s.Name, StringComparer.OrdinalIgnoreCase))
            .Where(s => s.IsInstalled())
            .ToList();

        var programsTask = Task.Run(() => programProvider.GetInstalledPrograms(options.IncludeSystemComponents), ct);

        var queries = enabled
            .Select(s => QuerySourceAsync(s, ct))
            .ToList();

        var programs = await programsTask;
        var results = await Task.WhenAll(queries);

        var upgrades = results.SelectMany(r => r.Upgrades).ToList();
        var warnings = results.Where(r => r.Warning is not null).Select(r => r.Warning!).ToList();
        var enabledNames = enabled.Select(s => s.Name).ToList();

        var rows = ReportMerger.Merge(programs, upgrades, enabledNames);
        return new ScanResult(rows, enabledNames, programs.Count, warnings);
    }

    private static async Task<(IReadOnlyList<UpgradeCandidate> Upgrades, string? Warning)> QuerySourceAsync(
        IPackageSource source, CancellationToken ct)
    {
        try
        {
            return (await source.ListOutdatedAsync(ct), null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ([], $"{source.Name} query failed — results may be incomplete ({ex.Message})");
        }
    }
}
