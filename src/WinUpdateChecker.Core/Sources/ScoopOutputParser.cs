using System.Text.RegularExpressions;
using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Sources;

/// <summary>Parses `scoop status` table output, ported from v1 Get-ScoopUpgrades.</summary>
public static partial class ScoopOutputParser
{
    public static IReadOnlyList<UpgradeCandidate> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        var lines = raw.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

        var headerIndex = Array.FindIndex(lines, l => HeaderPattern().IsMatch(l));
        if (headerIndex < 0) return [];

        var header = lines[headerIndex];
        var instCol = header.IndexOf("Installed Version", StringComparison.Ordinal);
        var latestCol = header.IndexOf("Latest Version", StringComparison.Ordinal);

        var upgrades = new List<UpgradeCandidate>();
        for (var i = headerIndex + 2; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (SeparatorPattern().IsMatch(line)) continue;
            if (line.Length < latestCol) continue;

            string name, current, available;
            try
            {
                name = line[..instCol].Trim();
                current = line[instCol..latestCol].Trim();
                available = TrailingColumns().Replace(line[latestCol..].Trim(), "");
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(name)) continue;
            if (string.IsNullOrWhiteSpace(available)) continue;
            if (current == available) continue;

            upgrades.Add(new UpgradeCandidate(name, name, current, available, "scoop"));
        }
        return upgrades;
    }

    [GeneratedRegex(@"^Name\s+Installed Version\s+Latest Version")]
    private static partial Regex HeaderPattern();

    [GeneratedRegex(@"^\s*[-]+\s*$")]
    private static partial Regex SeparatorPattern();

    [GeneratedRegex(@"\s+.*$")]
    private static partial Regex TrailingColumns();
}
