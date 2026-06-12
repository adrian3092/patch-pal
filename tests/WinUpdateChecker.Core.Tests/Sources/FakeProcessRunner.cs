using WinUpdateChecker.Core.Sources;

namespace WinUpdateChecker.Core.Tests.Sources;

public sealed class FakeProcessRunner : IProcessRunner
{
    public List<(string FileName, string Arguments, bool Elevated)> Calls { get; } = [];
    public string StdOut { get; set; } = "";
    public string StdErr { get; set; } = "";
    public int ExitCode { get; set; }
    public bool Exists { get; set; } = true;
    public Exception? ThrowOnRun { get; set; }

    public bool CommandExists(string command) => Exists;

    public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        if (ThrowOnRun is not null) throw ThrowOnRun;
        Calls.Add((fileName, arguments, false));
        return Task.FromResult(new ProcessResult(ExitCode, StdOut, StdErr));
    }

    public Task<ProcessResult> RunElevatedAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        if (ThrowOnRun is not null) throw ThrowOnRun;
        Calls.Add((fileName, arguments, true));
        return Task.FromResult(new ProcessResult(ExitCode, "", ""));
    }
}
