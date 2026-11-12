using System.Runtime.InteropServices;
using PatchPal.Core.Sources;

namespace PatchPal.Core.Upgrading;

/// <summary>
/// Dispatches an upgrade to the owning package source. Validates the package id, and the
/// repository the update comes from, BEFORE any process is launched (ported v1 security
/// fix b95dca8) and translates exit codes into human-readable messages.
/// On an ARM PC, a failed winget update is checked for a missing ARM64 installer: winget then
/// falls back to an x64 installer, which many apps (VPNs, disk encryption) refuse on ARM.
/// </summary>
public sealed class UpgradeRunner(IEnumerable<IPackageSource> sources, Architecture? machineArchitecture = null)
{
    private readonly Dictionary<string, IPackageSource> _sources =
        sources.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

    private readonly bool _isArmPc = (machineArchitecture ?? RuntimeInformation.OSArchitecture) == Architecture.Arm64;

    // Failures with a clear cause of their own: the installer was cancelled, there's no network,
    // or the UAC prompt was declined.
    private static readonly HashSet<int> OwnCauseExitCodes = [1602, -1978334969, -2147023673];

    internal const string NoArm64InstallerMessage =
        "winget has no ARM64 installer for this app, and the one it has can't install on this ARM PC. " +
        "Update it from within the app or with the publisher's ARM64 installer.";

    public async Task<UpgradeResult> UpgradeAsync(
        string source, string packageId, string displayName, string? repository = null, CancellationToken ct = default)
    {
        if (!PackageIdValidator.IsSafe(packageId))
            return new UpgradeResult(displayName, packageId, source, false, null,
                "Refused: package id contains unexpected characters.", "");

        if (repository is not null && !PackageIdValidator.IsSafe(repository))
            return new UpgradeResult(displayName, packageId, source, false, null,
                "Refused: package source name contains unexpected characters.", "");

        if (!_sources.TryGetValue(source, out var src))
            return new UpgradeResult(displayName, packageId, source, false, null,
                $"Unknown package source '{source}'.", "");

        try
        {
            var proc = await src.RunUpgradeAsync(packageId, repository, ct);
            var log = string.Join('\n', new[] { proc.StdOut, proc.StdErr }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
            var message = UpgradeExitMessages.Translate(source, proc.ExitCode);
            if (proc.ExitCode is { } code && code != 0 && await LacksArm64InstallerAsync(src, packageId, repository, code, ct))
            {
                log = $"{log}\n\n{source}: {message}".Trim();
                message = NoArm64InstallerMessage;
            }
            return new UpgradeResult(displayName, packageId, source,
                proc.ExitCode == 0, proc.ExitCode, message, log);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new UpgradeResult(displayName, packageId, source, false, null,
                "Elevation was declined (UAC prompt cancelled).", "");
        }
        catch (Exception ex)
        {
            // Most commonly the user declined the UAC elevation prompt.
            return new UpgradeResult(displayName, packageId, source, false, null, ex.Message, "");
        }
    }

    private async Task<bool> LacksArm64InstallerAsync(
        IPackageSource src, string packageId, string? repository, int exitCode, CancellationToken ct)
    {
        if (!_isArmPc || OwnCauseExitCodes.Contains(exitCode) || src is not IInstallerArchitectureCheck check) return false;
        // Microsoft Store listings show no installer hash, so only winget's own catalog can be asked.
        var catalog = repository ?? "winget";
        if (!catalog.Equals("winget", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            return await check.HasInstallerForAsync(packageId, catalog, "arm64", ct) == false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;   // the check is a courtesy; the installer's own reason still stands
        }
    }
}
