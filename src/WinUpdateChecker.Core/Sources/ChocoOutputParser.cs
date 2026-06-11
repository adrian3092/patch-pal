using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Sources;

/// <summary>Parses `choco outdated -r` pipe-delimited output, ported from v1 Get-ChocoUpgrades.</summary>
public static class ChocoOutputParser
{
    public static IReadOnlyList<UpgradeCandidate> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        var upgrades = new List<UpgradeCandidate>();
        foreach (var rawLine in raw.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split('|');
            if (parts.Length < 3) continue;
            var name = parts[0].Trim();
            upgrades.Add(new UpgradeCandidate(name, name, parts[1].Trim(), parts[2].Trim(), "chocolatey"));
        }
        return upgrades;
    }
}
