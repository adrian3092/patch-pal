using System.Text.RegularExpressions;

namespace PatchPal.Core.Matching;

/// <summary>Version parsing and comparison, ported from v1 Get-VersionValue / Test-IsNewerVersion.</summary>
public static partial class VersionLogic
{
    /// <summary>Parse a version-ish string, or 0.0 when it can't be parsed.</summary>
    public static Version GetVersionValue(string? text)
        => TryParse(text, out var v) ? v : new Version(0, 0);

    /// <summary>True when the text holds a comparable version (winget prints "Unknown" otherwise).</summary>
    public static bool IsParsable(string? text) => TryParse(text, out _);

    /// <summary>
    /// True when <paramref name="available"/> is strictly newer than <paramref name="current"/>.
    /// winget writes "&lt; X" for an install it only knows is older than X, so X itself counts
    /// as newer. When either side can't be parsed, assume an update IS available so real
    /// updates are never hidden by an odd version string.
    /// </summary>
    public static bool IsNewerVersion(string? current, string? available)
    {
        if (TryParse(current, out var c) && TryParse(available, out var a))
            return a > c || (a == c && IsBelow(current));
        return true;
    }

    private static bool IsBelow(string? version) => version?.TrimStart().StartsWith('<') == true;

    // System.Version counts a missing part as -1, which ranks "8.6" below "8.6.0"; fill missing
    // parts in with 0 so versions written with fewer parts compare equal. A whole number such as
    // "121" has no dot for System.Version to parse, so it reads as 121.0.
    private static bool TryParse(string? text, out Version version)
    {
        var token = Clean(text);
        if (token.Length > 0 && !token.Contains('.')) token += ".0";
        if (Version.TryParse(token, out var parsed))
        {
            version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0), Math.Max(parsed.Revision, 0));
            return true;
        }
        version = new Version(0, 0);
        return false;
    }

    private static string Clean(string? text)
    {
        // Deviation from v1: v1 stripped non-version chars globally, so "8.0.8 (x64)"
        // became "8.0.864" and hid real updates. Instead, replace non-version chars with
        // spaces and take the first dotted token, or failing that the first number.
        var tokens = NonVersionChars().Replace(text ?? "", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var token = tokens.FirstOrDefault(t => t.Contains('.')) ?? tokens.FirstOrDefault() ?? "";
        return token.Trim('.');
    }

    [GeneratedRegex(@"[^\d.]")]
    private static partial Regex NonVersionChars();
}
