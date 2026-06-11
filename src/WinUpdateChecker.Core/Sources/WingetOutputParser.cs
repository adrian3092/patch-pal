using System.Text.RegularExpressions;
using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Sources;

/// <summary>
/// Parses `winget upgrade` table output by header column positions,
/// ported from v1 Get-WingetUpgrades.
/// </summary>
public static partial class WingetOutputParser
{
    public static IReadOnlyList<UpgradeCandidate> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        // Winget uses bare \r (carriage return) as a line separator when stdout is
        // redirected (progress-spinner animation), so we split on any CR/LF combination.
        var lines = raw.Split(['\r', '\n'], StringSplitOptions.None)
                       .Select(l => l.TrimEnd('\r', '\n'))
                       .ToArray();

        var headerIndex = Array.FindIndex(lines, l => HeaderPattern().IsMatch(l));
        if (headerIndex < 0) return [];

        var header = lines[headerIndex];
        var idCol = header.IndexOf("Id", StringComparison.Ordinal);
        var versionCol = header.IndexOf("Version", StringComparison.Ordinal);
        var availableCol = header.IndexOf("Available", StringComparison.Ordinal);
        var sourceCol = header.IndexOf("Source", StringComparison.Ordinal);

        var upgrades = new List<UpgradeCandidate>();
        // Start just past the header; the dashed separator line (and any empty tokens
        // produced by splitting \r\n input) are skipped by the guards below.
        for (var i = headerIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (FooterPattern().IsMatch(line)) break;
            if (SeparatorPattern().IsMatch(line)) continue;
            if (line.Length < availableCol) continue;

            string name, id, current, available;
            try
            {
                name = line[..idCol].Trim();
                id = line[idCol..versionCol].Trim();
                current = line[versionCol..availableCol].Trim();
                available = sourceCol > 0 && line.Length >= sourceCol
                    ? line[availableCol..sourceCol].Trim()
                    : line[availableCol..].Trim();
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id)) continue;
            upgrades.Add(new UpgradeCandidate(name, id, current, available, "winget"));
        }
        return upgrades;
    }

    [GeneratedRegex(@"^Name\s+Id\s+Version\s+Available")]
    private static partial Regex HeaderPattern();

    [GeneratedRegex(@"^\s*\d+\s+upgrades available")]
    private static partial Regex FooterPattern();

    [GeneratedRegex(@"^\s*[-]+\s*$")]
    private static partial Regex SeparatorPattern();
}
