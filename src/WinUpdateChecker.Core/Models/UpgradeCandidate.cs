namespace WinUpdateChecker.Core.Models;

/// <summary>An outdated package reported by a package manager.</summary>
public sealed record UpgradeCandidate(string Name, string Id, string Current, string Available, string PackageSource);
