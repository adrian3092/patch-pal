using System.Text.RegularExpressions;

namespace PatchPal.Core.Sources;

/// <summary>
/// Reads `winget show --architecture X`. winget prints its labels and messages in the Windows
/// display language and exits 0 either way, so the parser reads what doesn't translate: the
/// package id in brackets on the first line, and the installer's SHA-256, which winget prints
/// only when it found an installer for that architecture.
/// </summary>
public static partial class WingetShowParser
{
    [GeneratedRegex(@"\b[0-9A-Fa-f]{64}\b")]
    private static partial Regex Sha256();

    /// <summary>True or false for the package's installer, or null when the output isn't about that package.</summary>
    public static bool? HasInstaller(string output, string packageId)
    {
        if (!output.Contains($"[{packageId}]", StringComparison.OrdinalIgnoreCase)) return null;
        return Sha256().IsMatch(output);
    }
}
