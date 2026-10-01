using System.Text.RegularExpressions;

namespace PatchPal.Core.Matching;

/// <summary>Version parsing and comparison, ported from v1 Get-VersionValue / Test-IsNewerVersion.</summary>
public static partial class VersionLogic
{
    /// <summary>Parse a version-ish string, or 0.0 when it can't be parsed.</summary>
    public static Version GetVersionValue(string? text)
        => Version.TryParse(Clean(text), out var v) ? v : new Version(0, 0);

    /// <summary>True when the text holds a comparable version (winget prints "Unknown" otherwise).</summary>
    public static bool IsParsable(string? text) => Version.TryParse(Clean(text), out _);

    /// <summary>
    /// True when <paramref name="available"/> is strictly newer than <paramref name="current"/>.
    /// winget writes "&lt; X" for an install it only knows is older than X, so X itself counts
    /// as newer. When either side can't be parsed, assume an update IS available so real
    /// updates are never hidden by an odd version string.
    /// </summary>
    public static bool IsNewerVersion(string? current, string? available)
    {
        if (Version.TryParse(Clean(current), out var c) && Version.TryParse(Clean(available), out var a))
            return a > c || (a == c && IsBelow(current));
        return true;
    }

    private static bool IsBelow(string? version) => version?.TrimStart().StartsWith('<') == true;

    private static string Clean(string? text)
    {
        // Deviation from v1: v1 stripped non-version chars globally, so "8.0.8 (x64)"
        // became "8.0.864" and hid real updates. Instead, replace non-version chars with
        // spaces and take the first dotted token.
        var replaced = NonVersionChars().Replace(text ?? "", " ");
        var token = replaced.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                             .FirstOrDefault(t => t.Contains('.')) ?? "";
        return token.Trim('.');
    }

    [GeneratedRegex(@"[^\d.]")]
    private static partial Regex NonVersionChars();
}
