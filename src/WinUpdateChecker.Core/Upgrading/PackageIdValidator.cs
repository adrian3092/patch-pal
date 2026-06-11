using System.Text.RegularExpressions;

namespace WinUpdateChecker.Core.Upgrading;

/// <summary>
/// Package ids are machine-generated identifiers (e.g. Microsoft.VCRedist.2015+.x64,
/// dotnet-sdk, git.install). Restrict to that charset so a crafted name can never inject
/// extra arguments or shell commands into an elevated installer call.
/// Whitelist, not blacklist: anything with a space, quote, ';', '&amp;', '$', '(', '`'
/// or other metacharacter is rejected.
/// </summary>
public static partial class PackageIdValidator
{
    public static bool IsSafe(string? id) => id is not null && SafeId().IsMatch(id);

    [GeneratedRegex(@"^[\w.+-]+$")]
    private static partial Regex SafeId();
}
