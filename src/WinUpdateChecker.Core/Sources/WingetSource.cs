using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Sources;

public sealed class WingetSource(IProcessRunner runner) : IPackageSource
{
    public string Name => "winget";
    public string ExecutableName => "winget";

    public bool IsInstalled() => runner.CommandExists(ExecutableName);

    public async Task<IReadOnlyList<UpgradeCandidate>> ListOutdatedAsync(CancellationToken ct = default)
    {
        var result = await runner.RunAsync("winget", "upgrade --include-unknown --accept-source-agreements", ct);
        return WingetOutputParser.Parse(result.StdOut);
    }

    public Task<ProcessResult> RunUpgradeAsync(string packageId, CancellationToken ct = default)
        => runner.RunElevatedAsync("winget",
            $"upgrade --id {packageId} --exact --source winget --accept-package-agreements --accept-source-agreements --disable-interactivity -h", ct);
}
