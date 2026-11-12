namespace PatchPal.Core.Sources;

/// <summary>A package manager that can say whether it has an installer for a given processor.</summary>
public interface IInstallerArchitectureCheck
{
    /// <summary>
    /// Whether <paramref name="repository"/> has an installer of <paramref name="packageId"/> for
    /// <paramref name="architecture"/> ("arm64", "x64"), or null when that can't be told.
    /// Both ids must already be validated with <see cref="Upgrading.PackageIdValidator"/>.
    /// </summary>
    Task<bool?> HasInstallerForAsync(string packageId, string repository, string architecture, CancellationToken ct = default);
}
