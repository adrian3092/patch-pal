using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Sources;

public sealed class ChocoSource(IProcessRunner runner) : IPackageSource
{
    public string Name => "chocolatey";
    public string ExecutableName => "choco";

    public bool IsInstalled() => runner.CommandExists(ExecutableName);

    public async Task<IReadOnlyList<UpgradeCandidate>> ListOutdatedAsync(CancellationToken ct = default)
    {
        var result = await runner.RunAsync("choco", "outdated -r --no-color", ct);
        var upgrades = ChocoOutputParser.Parse(result.StdOut);
        if (upgrades.Count == 0 && result.ExitCode != 0)
            throw new InvalidOperationException(SourceErrors.FirstLine(result.StdErr) ?? $"exit code {result.ExitCode}");
        return upgrades;
    }

    public Task<ProcessResult> RunUpgradeAsync(string packageId, CancellationToken ct = default)
        => runner.RunElevatedAsync("choco", $"upgrade {packageId} -y", ct);
}
