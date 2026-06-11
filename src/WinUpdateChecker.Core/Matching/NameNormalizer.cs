using System.Text.RegularExpressions;

namespace WinUpdateChecker.Core.Matching;

/// <summary>Fuzzy name normalization, ported from v1 Get-MatchBase / Test-BasePrefixMatch.</summary>
public static partial class NameNormalizer
{
    /// <summary>
    /// Normalize a program name for fuzzy matching. Strips a trailing dotted version
    /// (e.g. " - 14.44.35211") but PRESERVES edition tokens like "2013" or "2015-2022"
    /// so different product editions never collapse together.
    /// </summary>
    public static string GetMatchBase(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var b = name.ToLowerInvariant();
        b = TrailingDottedVersion().Replace(b, "");
        b = TrailingEllipsis().Replace(b, "");
        b = b.TrimEnd(' ', '-', '(', '[', '/', '+');
        return b.Trim();
    }

    /// <summary>
    /// True when two normalized bases are equal, or one is a word-boundary prefix of the
    /// other. The prefix path handles winget's truncated names without matching unrelated
    /// products (e.g. "Edge" vs "EdgeWebView2").
    /// </summary>
    public static bool IsBasePrefixMatch(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        if (a == b) return true;
        var (longer, shorter) = a.Length >= b.Length ? (a, b) : (b, a);
        if (shorter.Length < 6) return false;
        if (!longer.StartsWith(shorter, StringComparison.Ordinal)) return false;
        var next = longer[shorter.Length];
        return next is ' ' or '(';
    }

    [GeneratedRegex(@"\s*[-–]?\s*v?\d+\.\d[\d.]*\s*$")]
    private static partial Regex TrailingDottedVersion();

    [GeneratedRegex(@"…+$")]
    private static partial Regex TrailingEllipsis();
}
