namespace PatchPal.Core.Upgrading;

/// <summary>
/// Apps a package manager lists as upgradable but can never upgrade, because the app keeps
/// itself current. Windows installs Microsoft Edge with Edge's own setup.exe, while winget's
/// Microsoft.Edge package is MSI-only — winget refuses that cross-technology upgrade every
/// time (0x8A15008E), and Edge Update installs the new version on its own.
/// </summary>
public static class SelfUpdatingApps
{
    public static bool IsSelfUpdating(string source, string packageId)
        => string.Equals(source, "winget", StringComparison.OrdinalIgnoreCase)
           && string.Equals(packageId, "Microsoft.Edge", StringComparison.OrdinalIgnoreCase);
}
