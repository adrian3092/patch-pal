using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Sources;

/// <summary>A package manager that can report and apply upgrades.</summary>
public interface IPackageSource
{
    /// <summary>Canonical source name: "winget", "scoop", or "chocolatey".</summary>
    string Name { get; }

    /// <summary>The executable probed on PATH to detect the manager.</summary>
    string ExecutableName { get; }

    bool IsInstalled();

    /// <summary>Query the manager for outdated packages. Exceptions propagate; ScanService turns them into warnings.</summary>
    Task<IReadOnlyList<UpgradeCandidate>> ListOutdatedAsync(CancellationToken ct = default);

    /// <summary>
    /// Invoke the manager's upgrade command for a package id. Callers MUST validate the id
    /// with <see cref="Upgrading.PackageIdValidator"/> first — UpgradeRunner enforces this.
    /// </summary>
    Task<ProcessResult> RunUpgradeAsync(string packageId, CancellationToken ct = default);
}
