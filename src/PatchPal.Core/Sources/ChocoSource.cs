using PatchPal.Core.Models;

namespace PatchPal.Core.Sources;

/// <summary>
/// Chocolatey is a machine tool and upgrades run elevated, so choco.exe is used only by the full
/// path <see cref="IChocolateyLocator"/> vouches for, never found by name on PATH.
/// </summary>
public sealed class ChocoSource(IProcessRunner runner, IChocolateyLocator locator) : IPackageSource
{
    private const string NotTrusted = "Chocolatey isn't installed in a folder only an admin can change.";

    public string Name => "chocolatey";
    public string ExecutableName => "choco";

    public bool IsInstalled() => locator.ChocoExePath is not null;

    public async Task<IReadOnlyList<UpgradeCandidate>> ListOutdatedAsync(CancellationToken ct = default)
    {
        var choco = locator.ChocoExePath ?? throw new InvalidOperationException(NotTrusted);
        var result = await runner.RunAsync(choco, "outdated -r --no-color", ct, PinnedEnvironment());
        var upgrades = ChocoOutputParser.Parse(result.StdOut);
        if (upgrades.Count == 0 && result.ExitCode != 0)
            throw new InvalidOperationException(SourceErrors.FirstLine(result.StdErr) ?? $"exit code {result.ExitCode}");
        return upgrades;
    }

    public Task<ProcessResult> RunUpgradeAsync(string packageId, string? repository = null, CancellationToken ct = default)
    {
        var choco = locator.ChocoExePath;
        return choco is null
            ? Task.FromResult(new ProcessResult(-1, "", NotTrusted))
            : runner.RunElevatedAsync(choco, $"upgrade {packageId} -y", ct, PinnedEnvironment());
    }

    // choco.exe takes its home (config, helpers, extensions, package scripts) from ChocolateyInstall,
    // and a user-level variable of that name overrides the machine one in Patch Pal's environment.
    // Pin it to the folder the locator checked, so choco never loads code from anywhere else.
    private Dictionary<string, string?> PinnedEnvironment()
        => new() { ["ChocolateyInstall"] = locator.InstallDirectory };
}
