using System.Text.RegularExpressions;
using PatchPal.Core.Models;

namespace PatchPal.Core.Matching;

/// <summary>
/// Merges registry-installed programs with package-manager upgrade candidates,
/// ported from v1 Merge-ProgramsAndUpgrades / Resolve-DuplicatePackageRows.
/// Match order: exact name -> normalized base prefix -> word-boundary containment.
/// </summary>
public static class ReportMerger
{
    public static IReadOnlyList<ReportRow> Merge(
        IReadOnlyList<InstalledProgram> programs,
        IReadOnlyList<UpgradeCandidate> upgrades,
        IReadOnlyList<string> enabledSources)
    {
        var rows = new List<ReportRow>();
        var matchedKeys = new HashSet<string>();

        foreach (var prog in programs)
        {
            var (match, isExact) = FindMatch(prog, upgrades);
            if (match is not null)
            {
                matchedKeys.Add($"{match.PackageSource}|{match.Id}");
                // Only flag an update when the available version is actually newer than
                // what's installed. This stops phantom "updates" where winget keys off an
                // older wrapper entry than the runtime you already have.
                var installed = InstalledVersion(prog, match, isExact);
                var isNewer = VersionLogic.IsNewerVersion(installed, match.Available);
                rows.Add(new ReportRow(
                    Name: prog.Name,
                    Publisher: prog.Publisher,
                    Current: installed,
                    Available: isNewer ? match.Available : "",
                    Status: isNewer ? "Update available" : "Up to date",
                    PackageId: isNewer ? match.Id : "",
                    PackageSource: isNewer ? match.PackageSource : ""));
            }
            else
            {
                var status = enabledSources.Count > 0 ? "Not tracked" : "No package manager detected";
                rows.Add(new ReportRow(prog.Name, prog.Publisher, prog.Version, "", status, "", ""));
            }
        }

        foreach (var up in upgrades)
        {
            if (matchedKeys.Contains($"{up.PackageSource}|{up.Id}")) continue;
            rows.Add(new ReportRow(up.Name, "", up.Current, up.Available,
                $"Update available ({up.PackageSource} only)", up.Id, up.PackageSource));
        }

        return ResolveDuplicatePackageRows(rows)
            .OrderBy(r => r.IsUpdate ? 0 : 1)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The installed version to compare with the available one. winget names an installed
    /// program by its registry display name and reports its version in package numbering, which
    /// can differ from the registry's (the .NET SDK registers 8.4.2226.x for winget's 8.0.422),
    /// so an exact winget match uses winget's version when it reports one. Fuzzy matches —
    /// including names winget truncated with "…" — may be a newer entry than the one winget
    /// keyed off, and Scoop/Chocolatey versions are their own records, so those keep the
    /// registry version.
    /// </summary>
    private static string InstalledVersion(InstalledProgram prog, UpgradeCandidate match, bool isExact)
        => (isExact && match.PackageSource == "winget" && VersionLogic.IsParsable(match.Current))
            ? match.Current
            : prog.Version;

    private static (UpgradeCandidate? Match, bool IsExact) FindMatch(
        InstalledProgram prog, IReadOnlyList<UpgradeCandidate> upgrades)
    {
        foreach (var up in upgrades)
            if (string.Equals(up.Name, prog.Name, StringComparison.OrdinalIgnoreCase))
                return (up, true);

        // Guard fuzzy passes against empty names — mirrors v1 PowerShell truthiness check.
        // An empty prog.Name would build a \b\b regex that matches any upgrade name.
        if (string.IsNullOrEmpty(prog.Name)) return (null, false);

        var progBase = NameNormalizer.GetMatchBase(prog.Name);
        foreach (var up in upgrades)
        {
            if (string.IsNullOrEmpty(up.Name)) continue;
            if (NameNormalizer.IsBasePrefixMatch(progBase, NameNormalizer.GetMatchBase(up.Name)))
                return (up, false);
        }

        var progLower = prog.Name.ToLowerInvariant();
        foreach (var up in upgrades)
        {
            if (string.IsNullOrEmpty(up.Name)) continue;
            var upLower = up.Name.ToLowerInvariant();
            if (Regex.IsMatch(progLower, $@"\b{Regex.Escape(upLower)}\b")
                || Regex.IsMatch(upLower, $@"\b{Regex.Escape(progLower)}\b"))
                return (up, false);
        }
        return (null, false);
    }

    /// <summary>
    /// Collapse multiple installed entries that map to the same package id into a single
    /// "Update available" row (keeping the highest installed version), so stale leftover
    /// registry entries don't show as separate phantom updates.
    /// </summary>
    private static List<ReportRow> ResolveDuplicatePackageRows(List<ReportRow> rows)
    {
        var kept = new Dictionary<string, ReportRow>();
        var result = new List<ReportRow>();
        foreach (var r in rows)
        {
            if (!r.IsUpdate || string.IsNullOrWhiteSpace(r.PackageId))
            {
                result.Add(r);
                continue;
            }
            var key = $"{r.PackageSource}|{r.PackageId}";
            if (!kept.TryGetValue(key, out var existing))
            {
                kept[key] = r;
                result.Add(r);
            }
            else if (VersionLogic.GetVersionValue(r.Current) > VersionLogic.GetVersionValue(existing.Current))
            {
                result.Remove(existing);
                result.Add(r);
                kept[key] = r;
            }
        }
        return result;
    }
}
