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
        return ChocoOutputParser.Parse(result.StdOut);
    }

    public Task<ProcessResult> RunUpgradeAsync(string packageId, CancellationToken ct = default)
        => runner.RunElevatedAsync("choco", $"upgrade {packageId} -y", ct);
}
