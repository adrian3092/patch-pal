using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Sources;

public sealed class ScoopSource(IProcessRunner runner) : IPackageSource
{
    public string Name => "scoop";
    public string ExecutableName => "scoop";

    public bool IsInstalled() => runner.CommandExists(ExecutableName);

    public async Task<IReadOnlyList<UpgradeCandidate>> ListOutdatedAsync(CancellationToken ct = default)
    {
        var result = await runner.RunAsync("scoop", "status", ct);
        return ScoopOutputParser.Parse(result.StdOut);
    }

    // Scoop runs as the current user (no elevation). The id is interpolated into a
    // child-shell command, so it MUST stay constrained to the safe id charset enforced
    // by PackageIdValidator (UpgradeRunner refuses unsafe ids before reaching here).
    public Task<ProcessResult> RunUpgradeAsync(string packageId, CancellationToken ct = default)
        => runner.RunAsync("powershell", $"-NoProfile -Command \"scoop update {packageId}\"", ct);
}
