using WinUpdateChecker.Core.Models;
using WinUpdateChecker.Core.Scanning;
using WinUpdateChecker.Core.Sources;

namespace WinUpdateChecker.App.Tests;

public sealed class FakePrograms(params InstalledProgram[] programs) : IInstalledProgramProvider
{
    public IReadOnlyList<InstalledProgram> GetInstalledPrograms(bool includeSystemComponents) => programs;
}

public sealed class FakeRunner : IProcessRunner
{
    public int ExitCode { get; set; }
    public string ListOutput { get; set; } = "";
    public List<(string FileName, string Arguments, bool Elevated)> Calls { get; } = [];

    public bool CommandExists(string command) => true;

    public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        Calls.Add((fileName, arguments, false));
        return Task.FromResult(new ProcessResult(ExitCode, ListOutput, ""));
    }

    public Task<ProcessResult> RunElevatedAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        Calls.Add((fileName, arguments, true));
        return Task.FromResult(new ProcessResult(ExitCode, "", ""));
    }
}
