using System.Text.RegularExpressions;

namespace WinUpdateChecker.Core.Matching;

/// <summary>Version parsing and comparison, ported from v1 Get-VersionValue / Test-IsNewerVersion.</summary>
public static partial class VersionLogic
{
    /// <summary>Parse a version-ish string, or 0.0 when it can't be parsed.</summary>
    public static Version GetVersionValue(string? text)
        => Version.TryParse(Clean(text), out var v) ? v : new Version(0, 0);

    /// <summary>
    /// True when <paramref name="available"/> is strictly newer than <paramref name="current"/>.
    /// When either side can't be parsed, assume an update IS available so real updates
    /// are never hidden by an odd version string.
    /// </summary>
    public static bool IsNewerVersion(string? current, string? available)
    {
        if (Version.TryParse(Clean(current), out var c) && Version.TryParse(Clean(available), out var a))
            return a > c;
        return true;
    }

    private static string Clean(string? text)
    {
        var replaced = NonVersionChars().Replace(text ?? "", " ");
        var token = replaced.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                             .FirstOrDefault(t => t.Contains('.')) ?? "";
        return token.Trim('.');
    }

    [GeneratedRegex(@"[^\d.]")]
    private static partial Regex NonVersionChars();
}
