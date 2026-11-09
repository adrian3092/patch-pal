namespace PatchPal.Core.Models;

/// <summary>
/// An outdated package reported by a package manager. <paramref name="Repository"/> is where that
/// manager gets the update (winget's Source column: "winget", "msstore", or a custom source), or
/// null when the manager doesn't say.
/// </summary>
public sealed record UpgradeCandidate(
    string Name, string Id, string Current, string Available, string PackageSource, string? Repository = null);
