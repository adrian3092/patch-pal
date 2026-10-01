using PatchPal.Core.Models;

namespace PatchPal.Core.Sources;

public sealed class WingetSource(IProcessRunner runner) : IPackageSource
{
    public string Name => "winget";
    public string ExecutableName => "winget";

    public bool IsInstalled() => runner.CommandExists(ExecutableName);

    public async Task<IReadOnlyList<UpgradeCandidate>> ListOutdatedAsync(CancellationToken ct = default)
    {
        var result = await runner.RunAsync("winget", "upgrade --include-unknown --accept-source-agreements", ct);
        var upgrades = WingetOutputParser.Parse(result.StdOut);
        if (upgrades.Count > 0) return upgrades;
        // A non-zero exit with no parsable table means the query itself failed (e.g. the
        // winget alias couldn't be executed). Never report that as "no updates" — throw so
        // ScanService surfaces a warning instead.
        if (result.ExitCode != 0)
            throw new InvalidOperationException(SourceErrors.FirstLine(result.StdErr) ?? $"exit code {result.ExitCode}");
        // Likewise when winget printed a table the parser couldn't read (a format change).
        if (WingetOutputParser.ContainsTable(result.StdOut))
            throw new InvalidOperationException("couldn't read winget's list of updates");
        return upgrades;
    }

    public Task<ProcessResult> RunUpgradeAsync(string packageId, CancellationToken ct = default)
        => runner.RunElevatedAsync("winget",
            $"upgrade --id {packageId} --exact --source winget --accept-package-agreements --accept-source-agreements --disable-interactivity -h", ct);
}
