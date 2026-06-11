using WinUpdateChecker.Core.Sources;

namespace WinUpdateChecker.Core.Upgrading;

/// <summary>
/// Dispatches an upgrade to the owning package source. Validates the package id BEFORE
/// any process is launched (ported v1 security fix b95dca8) and translates exit codes
/// into human-readable messages.
/// </summary>
public sealed class UpgradeRunner(IEnumerable<IPackageSource> sources)
{
    private readonly Dictionary<string, IPackageSource> _sources =
        sources.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

    public async Task<UpgradeResult> UpgradeAsync(string source, string packageId, string displayName, CancellationToken ct = default)
    {
        if (!PackageIdValidator.IsSafe(packageId))
            return new UpgradeResult(displayName, packageId, source, false, null,
                "Refused: package id contains unexpected characters.", "");

        if (!_sources.TryGetValue(source, out var src))
            return new UpgradeResult(displayName, packageId, source, false, null,
                $"Unknown package source '{source}'.", "");

        try
        {
            var proc = await src.RunUpgradeAsync(packageId, ct);
            var log = string.Join('\n', new[] { proc.StdOut, proc.StdErr }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
            return new UpgradeResult(displayName, packageId, source,
                proc.ExitCode == 0, proc.ExitCode,
                UpgradeExitMessages.Translate(source, proc.ExitCode), log);
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
}
