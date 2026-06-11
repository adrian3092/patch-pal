using System.Text.RegularExpressions;
using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Matching;

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
            var match = FindMatch(prog, upgrades);
            if (match is not null)
            {
                matchedKeys.Add($"{match.PackageSource}|{match.Id}");
                // Only flag an update when the available version is actually newer than
                // what's installed. This stops phantom "updates" where winget keys off an
                // older wrapper entry than the runtime you already have.
                var isNewer = VersionLogic.IsNewerVersion(prog.Version, match.Available);
                rows.Add(new ReportRow(
                    Name: prog.Name,
                    Publisher: prog.Publisher,
                    Current: prog.Version,
                    Available: isNewer ? match.Available : "",
                    Status: isNewer ? "Update available" : "Up to date / unknown",
                    PackageId: isNewer ? match.Id : "",
                    PackageSource: isNewer ? match.PackageSource : ""));
            }
            else
            {
                var status = enabledSources.Count > 0 ? "Up to date / unknown" : "No package manager detected";
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

    private static UpgradeCandidate? FindMatch(InstalledProgram prog, IReadOnlyList<UpgradeCandidate> upgrades)
    {
        foreach (var up in upgrades)
            if (string.Equals(up.Name, prog.Name, StringComparison.OrdinalIgnoreCase))
                return up;

        var progBase = NameNormalizer.GetMatchBase(prog.Name);
        foreach (var up in upgrades)
        {
            if (string.IsNullOrEmpty(up.Name)) continue;
            if (NameNormalizer.IsBasePrefixMatch(progBase, NameNormalizer.GetMatchBase(up.Name)))
                return up;
        }

        var progLower = prog.Name.ToLowerInvariant();
        foreach (var up in upgrades)
        {
            if (string.IsNullOrEmpty(up.Name)) continue;
            var upLower = up.Name.ToLowerInvariant();
            if (Regex.IsMatch(progLower, $@"\b{Regex.Escape(upLower)}\b")
                || Regex.IsMatch(upLower, $@"\b{Regex.Escape(progLower)}\b"))
                return up;
        }
        return null;
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
