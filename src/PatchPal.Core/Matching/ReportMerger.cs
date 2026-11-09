using System.Text.RegularExpressions;
using PatchPal.Core.Models;

namespace PatchPal.Core.Matching;

/// <summary>
/// Merges registry-installed programs with package-manager upgrade candidates,
/// ported from v1 Merge-ProgramsAndUpgrades / Resolve-DuplicatePackageRows.
/// Match order: exact name -> normalized base prefix -> word-boundary containment. An update
/// that winget names after an installed program belongs to that program, so the fuzzy passes
/// only consider the others.
/// </summary>
public static class ReportMerger
{
    /// <summary>Status of an installed entry whose package's update is listed on another entry's row.</summary>
    public const string DuplicateEntryStatus = "Duplicate entry";

    public static IReadOnlyList<ReportRow> Merge(
        IReadOnlyList<InstalledProgram> programs,
        IReadOnlyList<UpgradeCandidate> upgrades,
        IReadOnlyList<string> enabledSources)
    {
        var rows = new List<ReportRow>();
        var matchedKeys = new HashSet<string>();

        // "Microsoft Edge WebView2 Runtime" begins with "Microsoft Edge", but an update winget
        // names "Microsoft Edge" is Edge's, so a program with another name can't take it.
        var openToFuzzyMatch = upgrades
            .Where(up => !string.IsNullOrEmpty(up.Name) && !programs.Any(p => SameName(p.Name, up.Name)))
            .ToList();

        foreach (var prog in programs)
        {
            var (match, isExact) = FindMatch(prog, upgrades, openToFuzzyMatch);
            if (match is not null)
            {
                matchedKeys.Add($"{match.PackageSource}|{match.Id}");
                // Only flag an update when the available version is actually newer than
                // what's installed. This stops phantom "updates" where winget keys off an
                // older wrapper entry than the runtime you already have.
                var installed = InstalledVersion(prog, match, isExact);
                var isNewer = VersionLogic.IsNewerVersion(installed, match.Available)
                              && !HasCurrentSibling(prog, match.Available, programs);
                rows.Add(new ReportRow(
                    Name: prog.Name,
                    Publisher: prog.Publisher,
                    Current: installed,
                    Available: isNewer ? match.Available : "",
                    Status: isNewer ? "Update available" : "Up to date",
                    PackageId: isNewer ? match.Id : "",
                    PackageSource: isNewer ? match.PackageSource : "",
                    Repository: isNewer ? match.Repository : null));
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
                $"Update available ({up.PackageSource} only)", up.Id, up.PackageSource, up.Repository));
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

    /// <summary>
    /// True when another registry entry for the same product (the same name but for a version on
    /// the end, as in "... (x64) - 14.51.36231") already has <paramref name="available"/>. An old
    /// installer left <paramref name="prog"/>'s entry behind, and winget can key the update off
    /// that leftover, though the product is current. Registry versions only count when the
    /// registry numbers the product as the package does, which the leftover's own version shows:
    /// it must be older than the update too (the .NET SDK, for one, numbers them differently).
    /// </summary>
    private static bool HasCurrentSibling(InstalledProgram prog, string available, IReadOnlyList<InstalledProgram> programs)
    {
        var product = NameNormalizer.GetMatchBase(prog.Name);
        return product.Length > 0
            && VersionLogic.IsNewerVersion(prog.Version, available)
            && programs.Any(other =>
                !ReferenceEquals(other, prog)
                && NameNormalizer.GetMatchBase(other.Name) == product
                && HasReleaseOf(other.Version, available));
    }

    // At or past the update within its release line (same major.minor): 14.51.36300 has
    // 14.51.36231, but a 3.12.1 install beside a 3.11 update is a release line of its own.
    private static bool HasReleaseOf(string installed, string available)
    {
        if (!VersionLogic.IsParsable(installed) || !VersionLogic.IsParsable(available)) return false;
        var have = VersionLogic.GetVersionValue(installed);
        var want = VersionLogic.GetVersionValue(available);
        return have.Major == want.Major && have.Minor == want.Minor && have >= want;
    }

    private static (UpgradeCandidate? Match, bool IsExact) FindMatch(
        InstalledProgram prog, IReadOnlyList<UpgradeCandidate> upgrades, IReadOnlyList<UpgradeCandidate> openToFuzzyMatch)
    {
        foreach (var up in upgrades)
            if (SameName(up.Name, prog.Name))
                return (up, true);

        // Guard fuzzy passes against empty names — mirrors v1 PowerShell truthiness check.
        // An empty prog.Name would build a \b\b regex that matches any upgrade name.
        if (string.IsNullOrEmpty(prog.Name)) return (null, false);

        var progBase = NameNormalizer.GetMatchBase(prog.Name);
        foreach (var up in openToFuzzyMatch)
        {
            if (NameNormalizer.IsBasePrefixMatch(progBase, NameNormalizer.GetMatchBase(up.Name)))
                return (up, false);
        }

        var progLower = prog.Name.ToLowerInvariant();
        foreach (var up in openToFuzzyMatch)
        {
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
    /// registry entries don't show as separate phantom updates. The other entries stay listed
    /// as duplicates, so All apps and the exports still show every installed program.
    /// </summary>
    private static List<ReportRow> ResolveDuplicatePackageRows(List<ReportRow> rows)
    {
        var kept = new Dictionary<string, ReportRow>();
        foreach (var r in rows.Where(IsPackageUpdate))
        {
            var key = PackageKey(r);
            if (!kept.TryGetValue(key, out var existing)
                || VersionLogic.GetVersionValue(r.Current) > VersionLogic.GetVersionValue(existing.Current))
                kept[key] = r;
        }

        return rows
            .Select(r => IsPackageUpdate(r) && !ReferenceEquals(kept[PackageKey(r)], r)
                ? r with { Available = "", Status = DuplicateEntryStatus, PackageId = "", PackageSource = "", Repository = null }
                : r)
            .ToList();
    }

    private static bool IsPackageUpdate(ReportRow row) => row.IsUpdate && !string.IsNullOrWhiteSpace(row.PackageId);

    private static string PackageKey(ReportRow row) => $"{row.PackageSource}|{row.PackageId}";

    private static bool SameName(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
