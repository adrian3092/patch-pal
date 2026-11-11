using PatchPal.Core.Models;

namespace PatchPal.Core.Sources;

public sealed class ScoopSource(IProcessRunner runner) : IPackageSource
{
    public string Name => "scoop";
    public string ExecutableName => "scoop";

    // where.exe only reads, so finding scoop doesn't need the de-elevated launch.
    public bool IsInstalled() => runner.CommandExists(ExecutableName);

    public async Task<IReadOnlyList<UpgradeCandidate>> ListOutdatedAsync(CancellationToken ct = default)
    {
        ProcessResult result;
        try
        {
            result = await RunScoopAsync("status", ct);
        }
        catch (ProcessLaunchException ex)
        {
            throw new InvalidOperationException("Couldn't start Scoop without admin rights, so its updates weren't checked.", ex);
        }
        var upgrades = ScoopOutputParser.Parse(result.StdOut);
        if (upgrades.Count == 0 && result.ExitCode != 0)
            throw new InvalidOperationException(SourceErrors.FirstLine(result.StdErr) ?? $"exit code {result.ExitCode}");
        return upgrades;
    }

    // The id is interpolated into a child-shell command, so it MUST stay constrained to the safe
    // id charset enforced by PackageIdValidator (UpgradeRunner refuses unsafe ids before reaching here).
    public async Task<ProcessResult> RunUpgradeAsync(string packageId, string? repository = null, CancellationToken ct = default)
    {
        try
        {
            return await RunScoopAsync($"update {packageId}", ct);
        }
        catch (ProcessLaunchException)
        {
            return new ProcessResult(-1, "",
                $"Couldn't start Scoop without admin rights; run 'scoop update {packageId}' from a normal prompt yourself.");
        }
    }

    // Scoop lives in the user's own folders, which any process of theirs can change, so it must
    // never run with Patch Pal's admin rights: it runs de-elevated, and if that launch fails it
    // doesn't run at all. Scoop is a PowerShell script with .cmd/.ps1 shims (no scoop.exe), so it
    // is started through System32's PowerShell by full path rather than searched for by name.
    private Task<ProcessResult> RunScoopAsync(string scoopArguments, CancellationToken ct)
        => runner.RunAsUserAsync(WindowsTools.PowerShellPath, $"-NoProfile -Command \"scoop {scoopArguments}\"", ct);
}
