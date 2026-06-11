using System.Text.RegularExpressions;

namespace WinUpdateChecker.Core.Scanning;

/// <summary>Inclusion rules for uninstall entries, ported from v1 Get-InstalledPrograms.</summary>
public static partial class RegistryEntryFilter
{
    public static bool ShouldInclude(RegistryEntry entry, bool includeSystemComponents)
    {
        if (string.IsNullOrWhiteSpace(entry.DisplayName)) return false;
        if (includeSystemComponents) return true;
        if (entry.SystemComponent == 1) return false;
        if (!string.IsNullOrEmpty(entry.ParentKeyName)) return false;
        if (entry.ReleaseType is "Update" or "Hotfix" or "Security Update") return false;
        if (HotfixStyleName().IsMatch(entry.DisplayName)) return false;
        return true;
    }

    [GeneratedRegex(@"^(KB\d+|Update for|Security Update|Hotfix)")]
    private static partial Regex HotfixStyleName();
}
