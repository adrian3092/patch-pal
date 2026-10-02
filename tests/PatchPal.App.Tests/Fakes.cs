using PatchPal.Core.Models;
using PatchPal.Core.Scanning;
using PatchPal.Core.Sources;

namespace PatchPal.App.Tests;

public sealed class FakePrograms(params InstalledProgram[] programs) : IInstalledProgramProvider
{
    public IReadOnlyList<InstalledProgram> GetInstalledPrograms(bool includeSystemComponents) => programs;
}

public sealed class FakeRunner : IProcessRunner
{
    public int ExitCode { get; set; }
    public string ListOutput { get; set; } = "";
    public List<(string FileName, string Arguments, bool Elevated)> Calls { get; } = [];

    /// <summary>When set, upgrades wait for it, so a test can look at the app mid-update.</summary>
    public TaskCompletionSource? UpgradeGate { get; set; }

    public bool CommandExists(string command) => true;

    public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        Calls.Add((fileName, arguments, false));
        return Task.FromResult(new ProcessResult(ExitCode, ListOutput, ""));
    }

    public async Task<ProcessResult> RunElevatedAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        Calls.Add((fileName, arguments, true));
        if (UpgradeGate is not null) await UpgradeGate.Task;
        return new ProcessResult(ExitCode, "", "");
    }
}
